namespace DriverLedger.Services
{
    /// <summary>
    /// MAUI Shell implementation of <see cref="IDialogService"/>.
    /// Registered as a singleton in MauiProgram.
    /// </summary>
    public class ShellDialogService : IDialogService
    {
        public Task ShowAlertAsync(string title, string message, string ok = "OK")
            => Shell.Current.DisplayAlertAsync(title, message, ok);

        public Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel = "Cancel")
            => Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
    }
}
