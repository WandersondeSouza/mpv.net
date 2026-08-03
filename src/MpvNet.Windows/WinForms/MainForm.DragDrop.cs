using System.Windows.Forms;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);

        if (e.Data!.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text))
            e.Effect = DragDropEffects.Copy;
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);

        bool append = ModifierKeys == Keys.Shift;

        if (e.Data!.GetDataPresent(DataFormats.FileDrop))
            Player.LoadFiles(
                ClipboardMediaParser.ParseFileDropList(e.Data.GetData(DataFormats.FileDrop) as string[], append)
                    .Select(request => request.Input).ToArray(), true, append);
        else if (e.Data.GetDataPresent(DataFormats.Text))
            Player.LoadFiles(
                ClipboardMediaParser.ParseText(e.Data.GetData(DataFormats.Text)!.ToString(), append)
                    .Select(request => request.Input).ToArray(), true, append);
    }
}
