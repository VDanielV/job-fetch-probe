namespace JobFetchProbe.Services.Abstractions;

/// <summary>The input folder of saved search pages. Implemented in Infrastructure on the file system.</summary>
public interface IInputFolder
{
    /// <summary>Full paths of the pages waiting in the input folder (not its subfolders), oldest first.</summary>
    IReadOnlyList<string> ListPending();

    /// <summary>Moves a page (and the "&lt;name&gt;_files" folder the browser saved next to it, if any) to the archive.</summary>
    /// <returns>The page's new full path.</returns>
    string Archive(string path);
}
