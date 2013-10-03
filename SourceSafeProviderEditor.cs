using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.SourceSafe
{
    internal sealed class SourceSafeProviderEditor : ProviderEditorBase
    {
        ValidatingTextBox txtUsername, txtPassword, txtRootPath, txtExePath, txtTimeout;
        SourceControlFileFolderPicker ctlDbPath;

        public SourceSafeProviderEditor()
        {
            ValidateBeforeSave += new System.EventHandler<ValidationEventArgs<ProviderBase>>(SourceSafeProviderEditor_ValidateBeforeSave);
        }

        void SourceSafeProviderEditor_ValidateBeforeSave(object sender, ValidationEventArgs<ProviderBase> e)
        {
            var prov = (SourceSafeProvider)e.Extension;
            if (prov.Timeout < 15) 
            {
                e.ValidLevel = ValidationLevel.Error;
                e.Message = "The timeout must be at least 15 seconds.";
            }
        }

        protected override void CreateChildControls()
        {
            txtExePath = new ValidatingTextBox() { Width = 300 };

            txtUsername = new ValidatingTextBox() { Width = 300 };

            txtPassword = new ValidatingTextBox() { Width = 300 };

            txtRootPath = new ValidatingTextBox() { Width = 300 };

            txtTimeout = new ValidatingTextBox() { Width = 100, DefaultText = "seconds", Type = System.Web.UI.WebControls.ValidationDataType.Integer };

            ctlDbPath = new SourceControlFileFolderPicker();
            ctlDbPath.ServerId = EditorContext.ServerId;

            CUtil.Add(this,
                new FormFieldGroup(
                    "SourceSafe Client",
                    "Specifies the location of the SourceSafe client.",
                    false,
                    new StandardFormField("Location of SS.EXE:", txtExePath)
                ),
                new FormFieldGroup(
                    "SourceSafe Login",
                    "The following fields can be used to connect to a SourceSafe database.  The values entered here "
                    + "may be the same as what are entered in the SourceSafe Windows client.<br /><br /> "
                    + "Depending on your SourceSafe configuration, a username and password may not be required.",
                    false,
                    new StandardFormField("Username:", txtUsername),
                    new StandardFormField("Password:", txtPassword)
                ),
                new FormFieldGroup(
                    "Database Connection",
                    "Use these options to specify the SourceSafe database file (srcsafe.ini) to connect to and (optionally) the root path within the database.",
                    false,
                    new StandardFormField("Database File Path:", ctlDbPath),
                    new StandardFormField("Database Root Path:", txtRootPath)
                ),
                new FormFieldGroup(
                    "Forced Timeout",
                    "SourceSafe's command line utility will open dialog boxes when certain errors occur or to prompt for user input, and there is no way to prevent them. "
                    + "Specify the amount of time (in seconds) to run the process before killing it so the build doesn't halt indefinitely if a dialog is opened. "
                    + "This value should be large enough to sustain the time it takes to run any actions using this provider, but must be greater than 15 seconds.",
                    false,
                    new StandardFormField("Forced Timeout:", txtTimeout)
                )
            );
        }

        public override void BindToForm(ProviderBase provider)
        {
            EnsureChildControls();

            SourceSafeProvider ssProvider = (SourceSafeProvider)provider;
            txtExePath.Text = ssProvider.UserDefinedSourceSafeClientExePath ?? string.Empty;
            txtUsername.Text = ssProvider.Username;
            txtPassword.Text = ssProvider.Password;
            txtTimeout.Text = (ssProvider.Timeout == 0) ? "30" : ssProvider.Timeout.ToString();
            ctlDbPath.Text = ssProvider.DbFilePath;
        }

        public override ProviderBase CreateFromForm()
        {
            EnsureChildControls(); 
            
            SourceSafeProvider ssProvider = new SourceSafeProvider();
            ssProvider.UserDefinedSourceSafeClientExePath = txtExePath.Text;
            ssProvider.Username = txtUsername.Text;
            ssProvider.Password = txtPassword.Text;
            ssProvider.DbFilePath = ctlDbPath.Text;
            ssProvider.Timeout = Util.Int.ParseZ(txtTimeout.Text.Trim());

            return (ProviderBase)ssProvider;
        }
    }
}