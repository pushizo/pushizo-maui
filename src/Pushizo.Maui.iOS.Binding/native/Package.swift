// swift-tools-version:5.9
// Pushizo .NET MAUI iOS köprüsü: yerel Swift SDK'sının (sdk/ios) @objc uyumlu cephesi. Dinamik framework olarak
// derlenir (build-xcframework.sh) ve Pushizo.Maui.iOS.Binding tarafından bağlanır. sdk/ios kaynakları değiştirilmez.
import PackageDescription

let package = Package(
    name: "PushizoMauiBridge",
    platforms: [.iOS(.v15)],
    products: [
        .library(name: "PushizoMauiBridge", type: .dynamic, targets: ["PushizoMauiBridge"]),
    ],
    dependencies: [
        // Paket kimliği klasör adından gelir ("ios").
        .package(path: "../../../../ios"),
    ],
    targets: [
        .target(
            name: "PushizoMauiBridge",
            dependencies: [.product(name: "Pushizo", package: "ios")],
            path: "Sources/PushizoMauiBridge"
        ),
    ]
)
