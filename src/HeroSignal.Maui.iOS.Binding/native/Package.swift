// swift-tools-version:5.9
// HeroSignal .NET MAUI iOS köprüleri: yerel HeroSignal SDK'sını (sdk/ios) Objective-C ABI ile dışa açan iki dinamik
// framework. build-xcframeworks.sh bunları XCFramework olarak ../build/xcframeworks altına üretir.
import PackageDescription

let package = Package(
    name: "HeroSignalMauiNative",
    platforms: [.iOS(.v15)],
    products: [
        // Uygulama hedefi: HSMauiBridge.
        .library(name: "HeroSignalMauiBridge", type: .dynamic, targets: ["HeroSignalMauiBridge"]),
        // Notification Service Extension hedefi: HSMauiNotificationService (uzantı güvenli API'ler).
        .library(name: "HeroSignalMauiNotificationService", type: .dynamic, targets: ["HeroSignalMauiNotificationService"]),
    ],
    dependencies: [
        // Paket kimliği yol klasörünün adıdır (sdk/ios → "ios").
        .package(path: "../../../../ios"),
    ],
    targets: [
        .target(
            name: "HeroSignalMauiBridge",
            dependencies: [.product(name: "HeroSignal", package: "ios")],
            path: "Sources/HeroSignalMauiBridge"
        ),
        .target(
            name: "HeroSignalMauiNotificationService",
            dependencies: [.product(name: "HeroSignalExtension", package: "ios")],
            path: "Sources/HeroSignalMauiNotificationService"
        ),
    ]
)
