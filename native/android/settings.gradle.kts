// Pushizo .NET MAUI Android köprüsü: yerel SDK (sdk/android/pushizo) kaynaktan bu derlemeye dahil edilir;
// köprü modülü (bridge) ona bağımlıdır. Çıktılar init betiğiyle depo dışına yönlendirilir
// (bkz. sdk/maui/tools/pushizo-native-out.init.gradle).
pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "pushizo-maui-native"

include(":pushizo")
project(":pushizo").projectDir = file("../../../android/pushizo")

include(":pushizo-maui-bridge")
project(":pushizo-maui-bridge").projectDir = file("bridge")
