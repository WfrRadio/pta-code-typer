import SwiftUI
import AppKit
import ApplicationServices
import UniformTypeIdentifiers

enum Section: String, CaseIterable, Identifiable {
    case code = "代码输入", permissions = "权限与环境", activity = "运行记录"
    var id: String { rawValue }
    var symbol: String {
        switch self { case .code: "curlybraces"; case .permissions: "lock.shield"; case .activity: "clock.arrow.circlepath" }
    }
    var tint: Color {
        switch self { case .code: .blue; case .permissions: .green; case .activity: .orange }
    }
}

final class EngineConnection: @unchecked Sendable {
    let process = Process()
    private let input = Pipe()
    private let output = Pipe()
    private let errorOutput = Pipe()
    private let lock = NSLock()
    private var buffer = Data()
    var receive: (([String: Any]) -> Void)?
    var exited: (() -> Void)?

    func launch() throws {
        guard let resources = Bundle.main.resourceURL else { throw CocoaError(.fileNoSuchFile) }
        let executable = resources.appendingPathComponent("Engine/PTAInputEngine")
        process.executableURL = executable
        process.standardInput = input
        process.standardOutput = output
        process.standardError = errorOutput
        output.fileHandleForReading.readabilityHandler = { [weak self] handle in
            let data = handle.availableData
            guard !data.isEmpty else { handle.readabilityHandler = nil; return }
            self?.consume(data)
        }
        // Drain stderr so a native framework diagnostic cannot block the worker.
        errorOutput.fileHandleForReading.readabilityHandler = { handle in
            if handle.availableData.isEmpty { handle.readabilityHandler = nil }
        }
        process.terminationHandler = { [weak self] _ in self?.exited?() }
        try process.run()
    }

    private func consume(_ data: Data) {
        lock.lock()
        buffer.append(data)
        var messages = [[String: Any]]()
        while let newline = buffer.firstIndex(of: 10) {
            let line = Data(buffer[..<newline])
            buffer.removeSubrange(...newline)
            if let message = try? JSONSerialization.jsonObject(with: line) as? [String: Any] { messages.append(message) }
        }
        lock.unlock()
        for message in messages { receive?(message) }
    }

    func send(_ request: [String: Any]) throws {
        var data = try JSONSerialization.data(withJSONObject: request)
        data.append(10)
        try input.fileHandleForWriting.write(contentsOf: data)
    }

    func close() {
        try? send(["command": "cancel"])
        try? input.fileHandleForWriting.close()
    }
}

struct LogEntry: Identifiable {
    let id = UUID()
    let date = Date()
    let message: String
    let error: Bool
}

@MainActor final class AppModel: ObservableObject {
    @Published var section: Section? = .code
    @Published var code = ""
    @Published var sourceName = "尚未载入代码"
    @Published var browser = "auto"
    @Published var delay = 0.045
    @Published var countdown = 7
    @Published var checkEvery = 1
    @Published var busy = false
    @Published var progress = 0.0
    @Published var status = "准备好代码，就可以开始。"
    @Published var statusSymbol = "circle.dotted"
    @Published var failed = false
    @Published var trusted = AXIsProcessTrusted()
    @Published var engineTrusted = false
    @Published var pythonVersion = "正在检测…"
    @Published var logs = [LogEntry]()
    private var engine: EngineConnection?
    var pendingTermination = false
    let reports = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        .appendingPathComponent("PTA Code Typer/reports", isDirectory: true)
    let browsers = [("auto", "自动识别"), ("safari", "Safari"), ("edge", "Microsoft Edge"),
                    ("chrome", "Google Chrome"), ("brave", "Brave"), ("chromium", "Chromium")]

    init() { launchEngine() }
    var version: String { Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "0.2.0" }
    var lineCount: Int { code.components(separatedBy: "\n").count }
    var canStart: Bool { !busy && !code.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }

    func launchEngine() {
        let connection = EngineConnection()
        connection.receive = { [weak self] message in
            DispatchQueue.main.async { self?.receive(message) }
        }
        connection.exited = { [weak self] in
            DispatchQueue.main.async {
                guard let self else { return }
                self.engine = nil
                if self.busy { self.showError("输入组件已退出。请检查作答内容，再重新开始。"); self.finish() }
            }
        }
        do { try connection.launch(); engine = connection; refresh() }
        catch { showError("无法启动内置输入组件：\(error.localizedDescription)") }
    }

    func refresh() {
        trusted = AXIsProcessTrusted()
        try? engine?.send(["command": "environment"])
    }

    func appendLog(_ message: String, error: Bool = false) {
        logs.append(LogEntry(message: message, error: error))
    }

    func showError(_ message: String) {
        status = message; statusSymbol = "exclamationmark.triangle.fill"; failed = true
        appendLog(message, error: true)
    }

    func loadClipboard() {
        guard !busy else { return }
        guard let text = NSPasteboard.general.string(forType: .string), !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            showError("剪贴板中没有代码文本。"); return
        }
        guard text.utf8.count <= 256 * 1024 else { showError("代码超过 256 KB，请缩小内容。"); return }
        code = text; sourceName = "来自剪贴板"; status = "代码已载入，可编辑或直接开始。"; failed = false; statusSymbol = "doc.text"
    }

    func importFile() {
        guard !busy else { return }
        let panel = NSOpenPanel()
        panel.title = "导入代码文件"; panel.canChooseDirectories = false; panel.allowsMultipleSelection = false
        panel.allowedContentTypes = [.sourceCode, .text, .plainText, .data]
        guard panel.runModal() == .OK, let url = panel.url else { return }
        loadFile(url)
    }

    func loadFile(_ url: URL) {
        guard !busy else { return }
        do {
            let data = try Data(contentsOf: url)
            guard data.count <= 256 * 1024 else { showError("文件超过 256 KB，请选择代码文件。"); return }
            guard var text = String(data: data, encoding: .utf8) else { showError("请将文件保存为 UTF-8 后再导入。"); return }
            if text.first == "\u{FEFF}" { text.removeFirst() }
            code = text; sourceName = url.lastPathComponent; status = "代码已载入，可编辑或直接开始。"; failed = false; statusSymbol = "doc.text"
        } catch { showError("无法读取文件：\(error.localizedDescription)") }
    }

    func openPermissions() {
        let key = kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String
        _ = AXIsProcessTrustedWithOptions([key: true] as CFDictionary)
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") { NSWorkspace.shared.open(url) }
    }

    func start() {
        guard canStart else { return }
        refresh()
        guard trusted else { section = .permissions; showError("请先允许 PTA Code Typer 使用辅助功能。"); return }
        guard code.utf8.count <= 256 * 1024 else { showError("代码超过 256 KB，请缩小内容。"); return }
        if engine == nil { launchEngine() }
        do {
            try engine?.send(["command": "start", "code": code,
                              "options": ["browser": browser, "delay": delay, "countdown": countdown, "check_every": checkEvery]])
            guard engine != nil else { return }
            busy = true; failed = false; progress = 0; statusSymbol = "timer"; section = .code
            status = "请在 \(countdown) 秒内点击 PTA 作答代码框。"
            appendLog("开始输入；请切换英文输入法，在倒计时内点击 PTA 代码框。")
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) { [weak self] in
                if self?.busy == true { NSApp.hide(nil) }
            }
        } catch { showError("无法开始输入：\(error.localizedDescription)") }
    }

    func cancel() {
        guard busy else { return }
        try? engine?.send(["command": "cancel"])
        status = "正在安全停止…"
    }

    func openReports() {
        do { try FileManager.default.createDirectory(at: reports, withIntermediateDirectories: true); NSWorkspace.shared.open(reports) }
        catch { showError("无法打开记录目录：\(error.localizedDescription)") }
    }

    func receive(_ message: [String: Any]) {
        guard let event = message["event"] as? String else { return }
        let value = message["value"]
        switch event {
        case "environment":
            if let info = value as? [String: Any] {
                engineTrusted = info["accessibility"] as? Bool ?? false
                pythonVersion = info["python"] as? String ?? "已内置"
            }
        case "countdown": status = "\(value as? Int ?? countdown) 秒后开始，请点击 PTA 作答代码框。"; statusSymbol = "timer"
        case "target":
            let info = value as? [String: Any]
            appendLog("已锁定 \(info?["browser"] as? String ?? "浏览器") 中的 PTA 编辑框。")
        case "progress":
            if let info = value as? [String: Any], let line = info["line"] as? Int, let total = info["total"] as? Int {
                progress = Double(line) / Double(max(total, 1)); status = "已输入 \(line) / \(total) 行，正在核对。"; statusSymbol = "keyboard"
            }
        case "backup": appendLog("原内容已备份：\(value as? String ?? "")")
        case "warning": appendLog(value as? String ?? "")
        case "report": appendLog("校验记录：\(value as? String ?? "")")
        case "attempt":
            if let info = value as? [String: Any] { appendLog("第 \(info["attempt"] as? Int ?? 1) 次输入。") }
        case "done":
            if let info = value as? [String: Any] {
                status = "完成：\(info["characters"] as? Int ?? 0) 个字符，全文回读一致。"
                statusSymbol = "checkmark.circle.fill"; progress = 1; failed = false; appendLog(status + " 未提交答案。")
            }
        case "error": showError(value as? String ?? "输入组件发生错误。")
        case "cancelled": status = value as? String ?? "已停止。"; statusSymbol = "stop.circle"; appendLog(status)
        case "finished": finish()
        default: break
        }
    }

    func finish() {
        busy = false
        NSApp.unhide(nil); NSApp.activate(ignoringOtherApps: true)
        NSApp.windows.first(where: { $0.canBecomeMain })?.makeKeyAndOrderFront(nil)
        refresh()
        if pendingTermination { NSApp.reply(toApplicationShouldTerminate: true) }
    }
}

struct CodeEditor: NSViewRepresentable {
    @Binding var text: String
    var editable: Bool
    func makeCoordinator() -> Coordinator { Coordinator(self) }
    func makeNSView(context: Context) -> NSScrollView {
        let scroll = NSScrollView()
        scroll.hasVerticalScroller = true; scroll.hasHorizontalScroller = true; scroll.borderType = .noBorder
        let editor = NSTextView()
        editor.isRichText = false; editor.allowsUndo = true
        editor.isAutomaticQuoteSubstitutionEnabled = false; editor.isAutomaticDashSubstitutionEnabled = false
        editor.isAutomaticTextReplacementEnabled = false; editor.isAutomaticSpellingCorrectionEnabled = false
        editor.font = .monospacedSystemFont(ofSize: 13, weight: .regular)
        editor.textColor = .textColor; editor.backgroundColor = .textBackgroundColor
        editor.textContainerInset = NSSize(width: 20, height: 18)
        editor.isVerticallyResizable = true; editor.isHorizontallyResizable = true
        editor.autoresizingMask = [.width]
        editor.textContainer?.containerSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
        editor.textContainer?.widthTracksTextView = false
        editor.minSize = NSSize(width: 0, height: 0); editor.maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
        editor.delegate = context.coordinator
        editor.setAccessibilityLabel("代码编辑器")
        scroll.documentView = editor
        return scroll
    }
    func updateNSView(_ scroll: NSScrollView, context: Context) {
        context.coordinator.parent = self
        guard let editor = scroll.documentView as? NSTextView else { return }
        if editor.string != text { editor.string = text }
        editor.isEditable = editable
    }
    class Coordinator: NSObject, NSTextViewDelegate {
        var parent: CodeEditor
        init(_ parent: CodeEditor) { self.parent = parent }
        func textDidChange(_ notification: Notification) {
            if let editor = notification.object as? NSTextView { parent.text = editor.string }
        }
    }
}

struct WorkspaceView: View {
    @ObservedObject var model: AppModel
    var body: some View {
        VStack(spacing: 0) {
            HStack(alignment: .top, spacing: 22) {
                VStack(alignment: .leading, spacing: 12) {
                    HStack {
                        Label(model.sourceName, systemImage: "doc.text").lineLimit(1)
                        Spacer()
                        Text("\(model.code.count) 字符 · \(model.lineCount) 行").monospacedDigit().foregroundStyle(.secondary)
                    }.font(.subheadline)
                    ZStack {
                        CodeEditor(text: $model.code, editable: !model.busy)
                        if model.code.isEmpty {
                            VStack(spacing: 12) {
                                Image(systemName: "curlybraces").font(.system(size: 38, weight: .light)).foregroundStyle(.tertiary)
                                Text("准备你的代码").font(.title3.weight(.semibold))
                                Text("读取剪贴板，或导入 UTF-8 代码文件。")
                                    .font(.subheadline).foregroundStyle(.secondary)
                            }.allowsHitTesting(false)
                        }
                    }
                    .clipShape(.rect(cornerRadius: 16))
                    .overlay(RoundedRectangle(cornerRadius: 16).strokeBorder(.quaternary))
                    HStack(spacing: 6) {
                        Image(systemName: "lock.shield")
                        Text("仅本地处理，完成后由你决定是否提交。")
                    }.font(.caption).foregroundStyle(.secondary)
                }.frame(maxWidth: .infinity, maxHeight: .infinity)
                options.frame(width: 210)
            }.padding(24)
            actionBar.padding(.horizontal, 24).padding(.bottom, 20)
        }
        .toolbar {
            ToolbarItemGroup(placement: .primaryAction) {
                Button(action: model.importFile) { Label("导入文件", systemImage: "square.and.arrow.down") }.help("导入代码文件（⌘O）")
                Button(action: model.loadClipboard) { Label("读取剪贴板", systemImage: "doc.on.clipboard") }.help("读取剪贴板中的代码（⇧⌘V）")
            }
        }
    }

    var options: some View {
        VStack(alignment: .leading, spacing: 24) {
            Text("输入设置").font(.headline)
            VStack(alignment: .leading, spacing: 8) {
                Text("浏览器").font(.subheadline).foregroundStyle(.secondary)
                Picker("浏览器", selection: $model.browser) {
                    ForEach(model.browsers, id: \.0) { item in Text(item.1).tag(item.0) }
                }.labelsHidden().frame(maxWidth: .infinity)
            }
            VStack(alignment: .leading, spacing: 8) {
                Text("字符间隔").font(.subheadline).foregroundStyle(.secondary)
                Picker("速度", selection: $model.delay) {
                    Text("标准 · 45 ms").tag(0.045)
                    Text("稳妥 · 70 ms").tag(0.07)
                    Text("较快 · 25 ms").tag(0.025)
                }.labelsHidden()
            }
            VStack(alignment: .leading, spacing: 8) {
                Text("开始倒计时").font(.subheadline).foregroundStyle(.secondary)
                Picker("倒计时", selection: $model.countdown) {
                    ForEach([5, 7, 10, 15, 20], id: \.self) { Text("\($0) 秒").tag($0) }
                }.labelsHidden()
            }
            VStack(alignment: .leading, spacing: 8) {
                Text("回读校验").font(.subheadline).foregroundStyle(.secondary)
                Picker("校验", selection: $model.checkEvery) { Text("逐行核对").tag(1); Text("每 5 行核对").tag(5) }.labelsHidden()
            }
            Divider()
            VStack(alignment: .leading, spacing: 10) {
                Label("开始前", systemImage: "cursorarrow.click").font(.subheadline.weight(.medium))
                Text("切换英文输入法。点击开始后，在倒计时内点击 PTA 作答代码框。")
                Text("原内容会先备份，再替换。")
            }.font(.caption).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 0)
            if !model.trusted {
                Button { model.section = .permissions } label: {
                    Label("设置辅助功能权限", systemImage: "exclamationmark.shield")
                }.buttonStyle(.link).font(.caption)
            }
        }.disabled(model.busy)
    }

    var actionBar: some View {
        HStack(spacing: 18) {
            Image(systemName: model.statusSymbol)
                .font(.title3).foregroundStyle(model.failed ? .orange : (model.progress == 1 ? .green : .blue))
            VStack(alignment: .leading, spacing: 5) {
                Text(model.status).font(.subheadline).lineLimit(2)
                if model.busy { ProgressView(value: model.progress).frame(maxWidth: 260) }
                else { Text("紧急停止：F8 / Fn+F8，或鼠标移至屏幕左上角").font(.caption).foregroundStyle(.secondary) }
            }
            Spacer(minLength: 12)
            if model.busy {
                Button("停止", systemImage: "stop.fill", action: model.cancel).buttonStyle(.glass).controlSize(.large)
            } else {
                Button("开始输入", systemImage: "play.fill", action: model.start)
                    .buttonStyle(.glassProminent).tint(.blue).controlSize(.large).disabled(!model.canStart)
            }
        }.padding(18).glassEffect(.regular, in: .rect(cornerRadius: 22))
    }
}

struct PermissionsView: View {
    @ObservedObject var model: AppModel
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 24) {
                HStack(spacing: 16) {
                    Image(systemName: model.trusted ? "checkmark.shield.fill" : "lock.shield")
                        .font(.system(size: 38, weight: .light)).foregroundStyle(model.trusted ? .green : .blue)
                    VStack(alignment: .leading, spacing: 6) {
                        Text(model.trusted ? "已获得辅助功能权限" : "允许 App 输入代码").font(.title2.weight(.semibold))
                        Text(model.trusted ? "可以开始准备和输入代码。" : "首次使用需要在系统设置中开启。")
                            .foregroundStyle(.secondary)
                    }
                }
                VStack(alignment: .leading, spacing: 16) {
                        Label("在辅助功能列表中开启 PTA Code Typer。", systemImage: "1.circle")
                        Label("如果没有此项，点击“＋”选择当前 App。", systemImage: "2.circle")
                        Label("返回这里，重新检测权限。", systemImage: "3.circle")
                        HStack {
                            Button("打开系统设置", systemImage: "gearshape", action: model.openPermissions).buttonStyle(.glassProminent)
                            Button("重新检测", systemImage: "arrow.clockwise", action: model.refresh).buttonStyle(.glass)
                        }.padding(.top, 4)
                }.frame(maxWidth: .infinity, alignment: .leading).padding(22)
                    .background(.background, in: .rect(cornerRadius: 16))
                    .overlay(RoundedRectangle(cornerRadius: 16).strokeBorder(.quaternary))
                VStack(alignment: .leading, spacing: 16) {
                    Text("运行环境").font(.headline)
                    environmentRow("界面", "原生 SwiftUI · Liquid Glass")
                    environmentRow("输入组件", "Python \(model.pythonVersion) · 已内置")
                    environmentRow("组件授权", model.engineTrusted ? "已允许" : "尚未允许，或需要重启 App")
                    environmentRow("版本", model.version)
                }.padding(22).background(.background, in: .rect(cornerRadius: 16))
                    .overlay(RoundedRectangle(cornerRadius: 16).strokeBorder(.quaternary))
                Label("首次识别 Safari、Edge 或 Chrome 时，系统可能询问自动化权限，用于读取当前标签页网址。", systemImage: "safari")
                    .foregroundStyle(.secondary).font(.subheadline)
                Text("更新后如果系统开关已开启，但仍检测不到权限，请退出 App，在列表中移除旧项，再添加当前 App。")
                    .foregroundStyle(.secondary).font(.caption)
            }.padding(32).frame(maxWidth: 720, alignment: .leading).frame(maxWidth: .infinity)
        }
    }
    func environmentRow(_ title: String, _ value: String) -> some View {
        HStack { Text(title).foregroundStyle(.secondary); Spacer(); Text(value) }
            .accessibilityElement(children: .combine)
    }
}

struct ActivityView: View {
    @ObservedObject var model: AppModel
    var body: some View {
        VStack(spacing: 0) {
            if model.logs.isEmpty {
                ContentUnavailableView("还没有运行记录", systemImage: "clock.arrow.circlepath", description: Text("开始输入后，可在这里查看备份和校验结果。"))
            } else {
                List(model.logs) { entry in
                    HStack(alignment: .top, spacing: 12) {
                        Image(systemName: entry.error ? "exclamationmark.circle" : "circle.fill")
                            .font(.caption).foregroundStyle(entry.error ? .orange : .blue).frame(width: 18)
                        VStack(alignment: .leading, spacing: 5) {
                            Text(entry.message).textSelection(.enabled)
                            Text(entry.date, style: .time).font(.caption).foregroundStyle(.secondary)
                        }
                    }.padding(.vertical, 6)
                }.listStyle(.inset)
            }
            HStack {
                Label("记录可能包含个人代码，请妥善保管。", systemImage: "lock").font(.caption).foregroundStyle(.secondary)
                Spacer()
                Button("打开记录文件夹", systemImage: "folder", action: model.openReports).buttonStyle(.glass)
            }.padding(20)
        }
    }
}

struct ContentView: View {
    @ObservedObject var model: AppModel
    var body: some View {
        NavigationSplitView {
            VStack(spacing: 0) {
                List(Section.allCases, selection: $model.section) { section in
                    Label {
                        Text(section.rawValue)
                    } icon: {
                        Image(systemName: section.symbol).foregroundStyle(section.tint)
                    }.tag(section)
                }.listStyle(.sidebar)
                HStack {
                    Image(systemName: "curlybraces").foregroundStyle(.blue)
                    Text("v\(model.version)").monospacedDigit()
                    Spacer()
                    Image(systemName: model.trusted ? "checkmark.shield" : "lock.shield")
                        .foregroundStyle(model.trusted ? .green : .secondary)
                }.font(.caption).foregroundStyle(.secondary).padding(18)
            }.navigationSplitViewColumnWidth(min: 180, ideal: 200, max: 230)
        } detail: {
            Group {
                switch model.section ?? .code {
                case .code: WorkspaceView(model: model)
                case .permissions: PermissionsView(model: model)
                case .activity: ActivityView(model: model)
                }
            }
            .navigationTitle((model.section ?? .code).rawValue)
            .navigationSubtitle("PTA Code Typer")
            .frame(minWidth: 720, minHeight: 600)
        }
        .onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in model.refresh() }
    }
}

@MainActor final class AppDelegate: NSObject, NSApplicationDelegate {
    weak var model: AppModel?
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        guard let model, model.busy else { return .terminateNow }
        model.pendingTermination = true; model.cancel(); return .terminateLater
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

@main struct PTACodeTyperApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    @StateObject private var model = AppModel()
    var body: some Scene {
        WindowGroup {
            ContentView(model: model)
                .onAppear { delegate.model = model }
        }
        .defaultSize(width: 1120, height: 760)
        .windowStyle(.automatic)
        .commands {
            CommandGroup(after: .newItem) {
                Button("导入代码文件…", action: model.importFile).keyboardShortcut("o").disabled(model.busy)
                Button("读取剪贴板", action: model.loadClipboard).keyboardShortcut("v", modifiers: [.command, .shift]).disabled(model.busy)
            }
            CommandGroup(replacing: .appSettings) {
                Button("权限与环境…") { model.section = .permissions }.keyboardShortcut(",")
            }
        }
    }
}
