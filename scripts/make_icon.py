"""Generate a code-native bracket icon with no image dependencies."""
from pathlib import Path
import math
import struct
import subprocess
import zlib

ROOT = Path(__file__).resolve().parent.parent

def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)

def distance(x, y, a, b):
    dx, dy = b[0]-a[0], b[1]-a[1]
    t = max(0, min(1, ((x-a[0])*dx+(y-a[1])*dy)/(dx*dx+dy*dy)))
    return math.hypot(x-a[0]-t*dx, y-a[1]-t*dy)

def png(size):
    paths = [((.36,.30),(.20,.50)),((.20,.50),(.36,.70)),
             ((.64,.30),(.80,.50)),((.80,.50),(.64,.70)),((.57,.25),(.43,.75))]
    rows=[]
    for yy in range(size):
        row=bytearray([0])
        y=(yy+.5)/size
        for xx in range(size):
            x=(xx+.5)/size
            qx,qy=abs(x-.5)-.29,abs(y-.5)-.29
            outside=math.hypot(max(qx,0),max(qy,0))+min(max(qx,qy),0)-.20
            alpha=max(0,min(1,.5-outside*size))
            ink=max(0,min(1,.5+(min(distance(x,y,*p) for p in paths)-.026)*-size))
            gradient=(int(55-26*y),int(126-46*y),int(248-24*y))
            color=tuple(int(c*(1-ink)+255*ink) for c in gradient)
            row.extend((*color,int(alpha*255)))
        rows.append(bytes(row))
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',size,size,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(rows),9))+chunk(b'IEND',b'')

if __name__=='__main__':
    folder=ROOT/'assets/AppIcon.iconset'; folder.mkdir(exist_ok=True)
    for n in [16,32,128,256,512]:
        (folder/f'icon_{n}x{n}.png').write_bytes(png(n))
        (folder/f'icon_{n}x{n}@2x.png').write_bytes(png(n*2))
    parts=[]
    for kind, filename in [(b'icp4','icon_16x16.png'),(b'icp5','icon_32x32.png'),
                           (b'ic07','icon_128x128.png'),(b'ic08','icon_256x256.png'),
                           (b'ic09','icon_512x512.png'),(b'ic10','icon_512x512@2x.png')]:
        data=(folder/filename).read_bytes()
        parts.append(kind+struct.pack('>I',len(data)+8)+data)
    body=b''.join(parts)
    (ROOT/'assets/app.icns').write_bytes(b'icns'+struct.pack('>I',len(body)+8)+body)
