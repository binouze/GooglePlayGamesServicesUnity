# GooglePlayGamesServicesUnity



### PROGUARD RULES 
```
# Garder les classes Google Sign In et Games
-keep class com.google.android.gms.auth.** { *; }
-keep class com.google.android.gms.games.** { *; }
-keep class com.google.android.gms.common.** { *; }
-keep class com.google.android.gms.tasks.** { *; }

# Nécessaire si on utilise la réflexion (rare ici, mais sécurité)
-keepnames class com.google.android.gms.auth.api.signin.** { *; }
```