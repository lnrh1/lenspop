import io
NL = chr(10)
p = 'src/Program.cs'
s = io.open(p, encoding='utf-8').read()
old = '            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LensPop-debug.log"), line); } catch { }'
new = ('            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LensPop-debug.log"), line); return; } catch { }' + NL +
       '            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Application.StartupPath, "debug-fallback.log"), line); } catch { }')
if old in s:
    s = s.replace(old, new, 1)
    io.open(p, 'w', encoding='utf-8', newline=chr(13)+chr(10)).write(s)
    print('ok')
else:
    print('missing')