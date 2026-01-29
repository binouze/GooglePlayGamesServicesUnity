using System.Collections.Generic;
using com.binouze.gpgs.Helpers;

namespace com.binouze.gpgs
{
    /// <summary> Information for the authenticated user.</summary>
    public class GPGSUser
    {
        /// <summary> Server AuthCode to be exchanged for an auth token.</summary>
        ///<remarks> null if not requested, or if there was an error.</remarks>
        public string AuthCode { get; internal set; }

        /// <summary> Email address.</summary>
        ///<remarks> null if not requested, or if there was an error.</remarks>
        public string Email { get; internal set; }

        /// <summary> Id token.</summary>
        ///<remarks> null if not requested, or if there was an error.</remarks>
        public string IdToken { get; internal set; }

        /// <summary> Display Name.</summary>
        /// <remarks>null by default, but you can set it if you use the authCode to get profile infos</remarks>
        public string DisplayName { get; internal set; }

        /// <summary> Given Name.</summary>
        /// <remarks>null by default, but you can set it if you use the authCode to get profile infos</remarks>
        public string GivenName { get; internal set; }

        /// <summary> Family Name.</summary>
        /// <remarks>null by default, but you can set it if you use the authCode to get profile infos</remarks>
        public string FamilyName { get; internal set; }

        /// <summary> Profile photo</summary>
        /// <remarks>null by default, but you can set it if you use the authCode to get profile infos</remarks>
        public string PhotoUrl { get; internal set; }

        /// <summary> User ID</summary>
        /// <remarks>null by default, but you can set it if you use the authCode to get profile infos</remarks>
        public string UserId { get; internal set; }

        ///<summary>User ID PlaysGameServices</summary>
        public string GPGSId { get; internal set; }

        public GPGSSignInStatusCode Status;

        public static GPGSUser FromObject( Dictionary<string,object> obj )
        {
            if( obj == null )
                return new GPGSUser { Status = GPGSSignInStatusCode.Error };

            return new GPGSUser
            {
                AuthCode    = obj.GetString( "AuthCode" ),
                Email       = obj.GetString( "Email" ),
                IdToken     = obj.GetString( "IdToken" ),
                DisplayName = obj.GetString( "DisplayName" ),
                FamilyName  = obj.GetString( "FamilyName" ),
                GivenName   = obj.GetString( "GivenName" ),
                PhotoUrl    = obj.GetString( "PhotoUrl" ),
                UserId      = obj.GetString( "UserId" ),
                GPGSId      = obj.GetString( "GPGSId" ),
                Status      = (GPGSSignInStatusCode)obj.GetInt( "Status" ),
            };
        }

        public GPGSUser( GPGSSignInStatusCode status = default, string authCode = null, string email = null, string idToken = null, string displayName = null, string givenName = null, string familyName = null, string photoUrl = null, string userId = null, string gpgsId = null )
        {
            Status      = status;
            AuthCode    = authCode;
            Email       = email;
            IdToken     = idToken;
            DisplayName = displayName;
            GivenName   = givenName;
            FamilyName  = familyName;
            PhotoUrl    = photoUrl;
            UserId      = userId;
            GPGSId      = gpgsId;
        }

        public override string ToString()
        {
            return $"{nameof( Status )}: {Status}, {nameof( AuthCode )}: {AuthCode}, {nameof( Email )}: {Email}, {nameof( IdToken )}: {IdToken}, {nameof( DisplayName )}: {DisplayName}, {nameof( GivenName )}: {GivenName}, {nameof( FamilyName )}: {FamilyName}, {nameof( PhotoUrl )}: {PhotoUrl}, {nameof( UserId )}: {UserId}, {nameof( GPGSId )}: {GPGSId}";
        }
    }
}