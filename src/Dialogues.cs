// Fusion PDF - sélecteur de dossier moderne (IFileOpenDialog, FOS_PICKFOLDERS).
//
// WPF sous .NET 4.8 n'a pas de sélecteur de dossier, et celui de WinForms
// (FolderBrowserDialog) est l'ancien arbre, sans barre d'adresse ni accès
// rapide. On appelle directement la boîte de dialogue de l'Explorateur.

using System;
using System.Runtime.InteropServices;

namespace FusionPDF
{
    public static class Dialogues
    {
        const uint FOS_PICKFOLDERS = 0x20;
        const uint FOS_FORCEFILESYSTEM = 0x40;
        const uint FOS_PATHMUSTEXIST = 0x800;
        const uint SIGDN_FILESYSPATH = 0x80058000;
        const int ERROR_CANCELLED_HR = unchecked((int)0x800704C7);

        /// <summary>Renvoie le dossier choisi, ou null si l'utilisateur annule.</summary>
        public static string ChoisirDossier(IntPtr parent, string titre, string dossierInitial)
        {
            IFileOpenDialog d = (IFileOpenDialog)new FileOpenDialogRCW();
            try
            {
                uint options;
                d.GetOptions(out options);
                d.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);
                d.SetTitle(titre);
                d.SetOkButtonLabel("Choisir ce dossier");
                if (!string.IsNullOrEmpty(dossierInitial))
                {
                    IShellItem initial;
                    Guid iid = typeof(IShellItem).GUID;
                    if (SHCreateItemFromParsingName(dossierInitial, IntPtr.Zero, ref iid, out initial) == 0 && initial != null)
                        d.SetFolder(initial);
                }

                int hr = d.Show(parent);
                if (hr == ERROR_CANCELLED_HR) return null;
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                IShellItem resultat;
                d.GetResult(out resultat);
                string chemin;
                resultat.GetDisplayName(SIGDN_FILESYSPATH, out chemin);
                return chemin;
            }
            finally
            {
                Marshal.ReleaseComObject(d);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid, out IShellItem ppv);

        [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        class FileOpenDialogRCW { }

        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(out IntPtr ppenum);
            void GetSelectedItems(out IntPtr ppsai);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }
    }
}
