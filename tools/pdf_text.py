import sys, zlib, re

def extract(path):
    data = open(path, 'rb').read()
    out = []
    # collect all stream sections
    for m in re.finditer(rb'stream\r?\n', data):
        start = m.end()
        end = data.find(b'endstream', start)
        if end == -1:
            continue
        raw = data[start:end]
        try:
            dec = zlib.decompress(raw)
        except Exception:
            dec = raw
        # extract text operators from content stream
        try:
            s = dec.decode('latin-1')
        except Exception:
            continue
        text = ''
        i = 0
        unescape = {'\\n':'\n','\\r':'\r','\\t':'\t','\\(':'(','\\)':')','\\\\':'\\'}
        while i < len(s):
            c = s[i]
            if c == '(':
                j = i + 1
                depth = 1
                buf = []
                while j < len(s) and depth > 0:
                    ch = s[j]
                    if ch == '\\':
                        esc = s[j:j+2]
                        buf.append(unescape.get(esc, s[j+1]))
                        j += 2
                    elif ch == '(':
                        depth += 1; buf.append('('); j += 1
                    elif ch == ')':
                        depth -= 1
                        if depth > 0: buf.append(')')
                        j += 1
                    else:
                        buf.append(ch); j += 1
                seg = ''.join(buf)
                # only keep segments followed by text-showing operators soon after
                tail = s[j:j+20]
                if re.match(r'\s*(Tj|TJ|\'|")', tail):
                    text += seg
                i = j
            elif c == 'T' and s[i:i+2] in ('Td', 'TD', 'T*'):
                if s[i:i+2] in ('TD', 'T*'):
                    text += '\n'
                else:
                    text += ' '
                i += 2
            else:
                i += 1
        if text.strip():
            out.append(text)
    return '\n'.join(out)

for p in sys.argv[1:]:
    print('===== ' + p + ' =====')
    print(extract(p))
