using System;
using System.Runtime.InteropServices;

namespace ArcherEditorMod.Editor.Tools;

/// <summary>Windows "open file" dialog (comdlg32). Returns null on other platforms or when cancelled.</summary>
public static class NativeFileDialog
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string? lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);

    private const int OFN_FILEMUSTEXIST = 0x1000;
    private const int OFN_PATHMUSTEXIST = 0x800;
    private const int OFN_NOCHANGEDIR = 0x8;

    /// <param name="filter">Pairs separated by \0, e.g. "meta.json\0meta.json\0All files\0*.*\0".</param>
    public static string? Open(string title, string filter)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        const int size = 1024;
        var buffer = Marshal.AllocHGlobal(size * sizeof(char));
        try
        {
            Marshal.WriteInt16(buffer, 0);
            var ofn = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                lpstrFilter = filter,
                nFilterIndex = 1,
                lpstrFile = buffer,
                nMaxFile = size,
                lpstrTitle = title,
                Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
            };

            return GetOpenFileName(ref ofn) ? Marshal.PtrToStringUni(buffer) : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
