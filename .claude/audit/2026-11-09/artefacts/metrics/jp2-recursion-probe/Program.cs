using System.Buffers.Binary;
using System.Reflection;
const int depth = 60000;
var data = new byte[depth * 8 + 12];
BinaryPrimitives.WriteInt32BigEndian(data, 12);
"jP  "u8.CopyTo(data.AsSpan(4));
new byte[] {13,10,135,10}.CopyTo(data,8);
for (int i=0;i<depth;i++) {
    int at=12+i*8;
    BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(at), data.Length-at);
    "jp2h"u8.CopyTo(data.AsSpan(at+4));
}
Console.WriteLine($"PROBE_START depth={depth} bytes={data.Length}");
var asm=Assembly.LoadFrom("/repo/src/OmniEurope.Documents/bin/Release/net10.0/OmniEurope.Documents.dll");
try {
    asm.GetType("OmniEurope.Documents.Imaging.Jpeg2000.Jp2File")!.GetMethod("Read")!.Invoke(null,new object[]{data});
    Console.WriteLine("PROBE_RETURNED");
} catch (TargetInvocationException e) {
    Console.WriteLine($"PROBE_MANAGED_EXCEPTION {e.InnerException!.GetType().Name}: {e.InnerException.Message}");
}
