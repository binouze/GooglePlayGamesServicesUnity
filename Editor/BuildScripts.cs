#if UNITY_ANDROID
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Xml;

namespace com.binouze.gpgs.Editor
{
    public class BuildScripts : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;

        public void OnPreprocessBuild( BuildReport report )
        {
            Debug.Log( "AdsPrebuildScript.OnPreprocessBuild for target " + report.summary.platform + " at path " + report.summary.outputPath );

            // creer les settings
            var settings = GPGSSettings.LoadSettingsInstance();
            if( settings == null || !settings.IsValid() )
            {
                Debug.LogError( "settings not valid please check LagoonPlugins/SignInWithAppleOrGoogle Settings" );
            }


            InjectMetaDataInManifest( settings.GPGS_ID, settings.AutoSignIn );
        }

        /*private void InjectMetaDataInManifest( string projectId )
        {
            var manifestPath = Path.Combine( Application.dataPath, "Plugins/Android/AndroidManifest.xml" );

            if( !File.Exists( manifestPath ) )
            {
                Debug.LogWarning(
                    "[GooglePlayServices] No AndroidManifest.xml found in Plugins/Android. Ensure you have one or the Meta-Data won't be added automatically to the main manifest." );
                return;
            }

            var doc = new XmlDocument();
            doc.Load( manifestPath );

            var applicationNode = doc.SelectSingleNode( "/manifest/application" );
            if( applicationNode == null ) return;

            var ns        = new XmlNamespaceManager( doc.NameTable );
            var androidNs = "http://schemas.android.com/apk/res/android";
            ns.AddNamespace( "android", androidNs );

            // creer le node xml si il n'existe pas
            if( applicationNode.SelectSingleNode( "meta-data[@android:name='com.google.android.gms.games.APP_ID']", ns ) is not XmlElement metaNode )
            {
                metaNode = doc.CreateElement("meta-data");
                metaNode.SetAttribute("name", androidNs, "com.google.android.gms.games.APP_ID");
                applicationNode.AppendChild(metaNode);
                Debug.Log($"[GooglePlayServices] Created new meta-data for APP_ID.");
            }

            var formattedId = "\\ " + projectId;
            metaNode.SetAttribute("value", androidNs, formattedId);

            doc.Save(manifestPath);
            Debug.Log($"[GooglePlayServices] Manifest updated with APP_ID: {projectId}");
        }*/

        private static void InjectMetaDataInManifest( string projectId, bool autoSignIn )
        {
            var manifestPath = Path.Combine( Application.dataPath, "Plugins/Android/AndroidManifest.xml" );

            if( !File.Exists( manifestPath ) )
            {
                Debug.LogError( "[GooglePlayServices] No AndroidManifest.xml found." );
                return;
            }

            var doc = new XmlDocument();
            doc.Load( manifestPath );

            var applicationNode = doc.SelectSingleNode( "/manifest/application" ) as XmlElement;
            if( applicationNode == null ) return;

            var ns        = new XmlNamespaceManager( doc.NameTable );
            var androidNs = "http://schemas.android.com/apk/res/android";
            var toolsNs   = "http://schemas.android.com/tools";
            ns.AddNamespace( "android", androidNs );
            ns.AddNamespace( "tools",   toolsNs );

            // --- Gestion de l'APP_ID ---
            var metaNode = applicationNode.SelectSingleNode( "meta-data[@android:name='com.google.android.gms.games.APP_ID']", ns ) as XmlElement;
            if( metaNode == null )
            {
                metaNode = doc.CreateElement( "meta-data" );
                metaNode.SetAttribute( "name", androidNs, "com.google.android.gms.games.APP_ID" );
                applicationNode.AppendChild( metaNode );
            }

            metaNode.SetAttribute( "value", androidNs, "\\ " + projectId );

            // --- Gestion du Auto Sign-In (Provider) ---
            var providerName = "com.google.android.gms.games.provider.PlayGamesInitProvider";
            var providerNode = applicationNode.SelectSingleNode( $"provider[@android:name='{providerName}']", ns ) as XmlElement;

            if( !autoSignIn )
            {
                // Si on veut désactiver, on crée ou met à jour le node avec tools:node="remove"
                if( providerNode == null )
                {
                    providerNode = doc.CreateElement( "provider" );
                    providerNode.SetAttribute( "name", androidNs, providerName );
                    applicationNode.AppendChild( providerNode );
                }

                // Configuration des attributs requis pour le provider
                providerNode.SetAttribute( "authorities", androidNs, "${applicationId}.playgamesinitprovider" );
                providerNode.SetAttribute( "exported",    androidNs, "false" );
                // L'astuce magique : tools:node="remove"
                providerNode.SetAttribute( "node", toolsNs, "remove" );

                Debug.Log( "[GooglePlayServices] Added provider removal to disable auto sign-in." );
            }
            else
            {
                // Si autoSignIn est vrai, on supprime carrément le nœud "remove" s'il existe
                if( providerNode != null )
                {
                    applicationNode.RemoveChild( providerNode );
                    Debug.Log( "[GooglePlayServices] Removed provider-disable node to allow default auto sign-in." );
                }
            }

            doc.Save( manifestPath );
        }
    }
}
#endif