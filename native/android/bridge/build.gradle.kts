// .NET MAUI binding'inin bağladığı Java dostu köprü: yalnızca metin/JSON, bool, int ve arayüz geri çağrıları kullanır.
plugins {
    id("com.android.library")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.herosignal.maui"
    compileSdk = 35

    defaultConfig {
        minSdk = 23
        consumerProguardFiles("consumer-rules.pro")
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }
}

dependencies {
    // Yerel SDK ayrı AAR olarak pakete girer (binding projesinde Bind=false); burada yalnızca derleme bağımlılığıdır.
    compileOnly(project(":herosignal"))
    compileOnly("androidx.core:core-ktx:1.15.0")
}
