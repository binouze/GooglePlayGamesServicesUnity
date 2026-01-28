namespace com.binouze.gpgs
{
    /// <summary>
    /// Configuration properties for GPGS Sign-In.
    /// </summary>
    public class GPGSConfiguration
    {
        /// <summary>Web client id associated with this app.</summary>
        /// <remarks>Required for requesting auth code.</remarks>
        internal string WebClientId   = null;
        /// <summary>Set to true for getting an auth code when authenticating.
        /// </summary>
        public bool RequestAuthCode = false;
        /// <summary>Request email address, requires consent.</summary>
        public bool RequestEmail    = false;
        /// <summary>Request profile, requires consent.</summary>
        public bool RequestProfile  = false;
        /// <summary>
        /// true if auto sign in is enabled in the settings
        /// </summary>
        internal bool AutoSignIn  = false;
    }
}