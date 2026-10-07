// HeroSignal .NET MAUI Android köprüsü: yerel SDK (sdk/android/herosignal) kaynaktan bu derlemeye dahil edilir;
// köprü modülü (bridge) ona bağımlıdır. Çıktılar init betiğiyle depo dışına yönlendirilir
// (bkz. sdk/maui/tools/herosignal-native-out.init.gradle).
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

rootProject.name = "herosignal-maui-native"

include(":herosignal")
project(":herosignal").projectDir = file("../../../android/herosignal")

include(":herosignal-maui-bridge")
project(":herosignal-maui-bridge").projectDir = file("bridge")
