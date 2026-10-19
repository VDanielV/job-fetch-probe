using JobFetchProbe.Data.Applications.Entities;
using JobFetchProbe.Models.Applications;

namespace JobFetchProbe.Data.Applications;

/// <summary>Copies a timeline row to the model; the kind is stored by name.</summary>
internal static class ApplicationRowMapping
{
    public static ApplicationNote ToModel(this ApplicationNoteRow row)
    {
        return new ApplicationNote(row.Id, row.At, Enum.Parse<ApplicationNoteKind>(row.Kind), row.Text);
    }
}
