// swift-tools-version:5.9
// HeroSignal .NET MAUI iOS köprüsü: yerel Swift SDK'sının (sdk/ios) @objc uyumlu cephesi. Dinamik framework olarak
// derlenir (build-xcframework.sh) ve HeroSignal.Maui.iOS.Binding tarafından bağlanır. sdk/ios kaynakları değiştirilmez.
import PackageDescription

let package = Package(
    name: "HeroSignalMauiBridge",
    platforms: [.iOS(.v15)],
    products: [
        .library(name: "HeroSignalMauiBridge", type: .dynamic, targets: ["HeroSignalMauiBridge"]),
    ],
    dependencies: [
        // Paket kimliği klasör adından gelir ("ios").
        .package(path: "../../../../ios"),
    ],
    targets: [
        .target(
            name: "HeroSignalMauiBridge",
            dependencies: [.product(name: "HeroSignal", package: "ios")],
            path: "Sources/HeroSignalMauiBridge"
        ),
    ]
)
