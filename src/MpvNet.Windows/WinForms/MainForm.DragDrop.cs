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
            Player.LoadFiles(e.Data.GetData(DataFormats.FileDrop) as string[], true, append);
        else if (e.Data.GetDataPresent(DataFormats.Text))
            Player.LoadFiles(new[] { e.Data.GetData(DataFormats.Text)!.ToString()! }, true, append);
    }
}
