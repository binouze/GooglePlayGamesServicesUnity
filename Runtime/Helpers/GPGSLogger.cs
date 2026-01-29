using UnityEngine;

namespace com.binouze.gpgs.Helpers
{
    public static class GPGSLogger
    {
        private static bool Enabled { get; set; }

        internal static void SetEnabled( bool enabled )
        {
            Enabled = enabled;
        }
        
        internal static void Log( string value )
        {
            if( Enabled )
                Debug.Log( $"[GooglePlayServices] {value}" );
        }

        internal static void LogWarning( string value )
        {
            if( Enabled )
                Debug.LogWarning( $"[GooglePlayServices] {value}" );
        }
        
        internal static void LogError( string value )
        {
            Debug.LogError( $"[GooglePlayServices] {value}" );
        }
    }
}