# Tedd.WritableBitmap
TODO: Documentation

# Speed

Why is this faster than [System.Windows.Media.Imaging.WriteableBitmap](https://docs.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.writeablebitmap)?

.Net is all about safety. Safety comes at a cost.

```c#
public byte[] array;
public void ModifyIt() {
    array[5] = 1;
}
```
Is compiled down to:

```asm
L0000: mov eax, [ecx+4]
L0003: cmp dword ptr [eax+4], 5
L0007: jbe short L000e
L0009: mov byte ptr [eax+0xd], 1
L000d: ret
L000e: call 0x0fa32000
L0013: int3
```
Lets do the same with an unsafe pointer.

```c#
public byte* array;
public void ModifyIt() {
    array[5] = 1;
}
```
This compiles to:

```assembly
L0000: mov eax, [ecx+4]
L0003: mov byte ptr [eax+5], 1
L0007: ret
```
You don't have to understand exactly what is going on here, the short version is: Fewer instructions, no branching. The slightly longer explanation is that when accessing an array, .Net will check every access you make (read or write) to ensure you are not out of bounds. This holds true even with Span&lt;T&gt;.

If you ware willing to take on some risk you can get some reward.

