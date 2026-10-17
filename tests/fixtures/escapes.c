#define SUM(a, b) \
    ((a) + (b))
/* comment: ()[]{} " '
 * keep exactly one star
 */
char quote = '\'';
const char *text = "中文 \\ \t \" []{}()";
int value = SUM(1, 2);
