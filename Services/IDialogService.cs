namespace DriverLedger.Services
{
    /// <summary>
    /// Abstracts alert/confirm dialogs so ViewModels never reference Shell.Current directly.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>Shows a one-button informational alert.</summary>
        Task ShowAlertAsync(string title, string message, string ok = "OK");

        /// <summary>Shows a two-button confirm dialog. Returns true if the user tapped <paramref name="accept"/>.</summary>
        Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel = "Cancel");
    }
}
