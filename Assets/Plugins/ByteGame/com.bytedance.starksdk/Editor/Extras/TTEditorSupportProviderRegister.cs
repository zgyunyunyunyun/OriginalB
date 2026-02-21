using UnityEditor;

namespace TTSDK.Tool
{
    [InitializeOnLoad]
    public class TTEditorSupportProviderRegister
    {
        private const string RegisterFlagKey = "OriginalB.TTEditorSupportProvider.Registered";

        static TTEditorSupportProviderRegister()
        {
            if (SessionState.GetBool(RegisterFlagKey, false))
                return;

            if (TTEditorSupportProvider.Android == null)
                TTEditorSupportProvider.RegisterAndroidSupportProvider(new TTAndroidSupportProvider());

            if (TTEditorSupportProvider.MiniGame == null)
                TTEditorSupportProvider.RegisterMiniGameSupportProvider(new TTMiniGameSupportProvider());

            SessionState.SetBool(RegisterFlagKey, true);
        }

    }
}