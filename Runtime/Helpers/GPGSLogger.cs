using UnityEngine;

namespace com.binouze.gpgs.Helpers
{
    public static class GPGSLogger
    {
        [RuntimeInitializeOnLoadMethod]
        private static void RuntimeInitializeOnLoad()
        {
            Enabled = false;
        }
        
        private static bool Enabled;

        internal static void SetEnabled( bool enabled )
        {
            Enabled = enabled;
        }
        
        internal static void Log( string value )
        {
            if( Enabled )
                Debug.Log( $"[SignInWithAppleOrGoogle] {value}" );
        }

        internal static void LogWarning( string value )
        {
            if( Enabled )
                Debug.LogWarning( $"[SignInWithAppleOrGoogle] {value}" );
        }
        
        internal static void LogError( string value )
        {
            if( Enabled )
                Debug.LogError( $"[SignInWithAppleOrGoogle] {value}" );
        }
    }
}