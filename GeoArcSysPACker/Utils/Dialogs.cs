using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace GeoArcSysPACker.Utils
{
    public static class Dialogs
    {
        public static string OpenFileDialog(string Title, string Filter = "All files|*.*")
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Title = Title;
            openFileDialog.Filter = Filter;
            if (openFileDialog.ShowDialog() == true)
                return openFileDialog.FileName;
            return null;
        }

        public static string OpenFolderDialog(string Title, string FolderName = null)
        {
            var dlg = new CommonOpenFileDialog();
            dlg.Title = Title;
            dlg.IsFolderPicker = true;
            dlg.DefaultFileName = FolderName;

            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.EnsurePathExists = false;
            dlg.EnsureFileExists = false;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok) return dlg.FileName;

            return null;
        }
    }
}