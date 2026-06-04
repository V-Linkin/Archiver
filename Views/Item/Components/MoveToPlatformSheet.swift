import SwiftUI

struct MoveToPlatformSheet: View {
    let itemID: UUID?
    var itemIDs: [UUID] = []
    @Binding var isPresented: Bool
    var onMoved: (() -> Void)?
    @Environment(AppState.self) private var appState
    private let itemService = ItemService()
    @State private var selectedPlatformID: UUID? = nil
    @State private var item: Item?

    
    var body: some View {
        VStack(spacing: 0) {
            Text("移动到平台")
                .font(.headline)
                .padding()

            
            Divider()
            
            ScrollView {
                VStack(spacing: 0) {
                    ForEach(appState.customPlatforms) { cp in
                        Button {
                            selectedPlatformID = cp.id
                        } label: {
                            HStack {
                                if let logoPath = cp.logoPath {
                                    let url = DataDirectory.platformLogos.appendingPathComponent(logoPath)
                                    if let nsImage = NSImage(contentsOf: url) {
                                        Image(nsImage: nsImage)
                                            .resizable()
                                            .frame(width: 20, height: 20)
                                            .clipShape(RoundedRectangle(cornerRadius: 4))
                                    } else {
                                        Image(systemName: "star.fill").foregroundStyle(.purple)
                                    }
                                } else {
                                    Image(systemName: "star.fill").foregroundStyle(.purple)
                                }
                                Text(cp.name)
                                Spacer()
                                if selectedPlatformID == cp.id {
                                    Image(systemName: "checkmark").foregroundStyle(.blue)
                                }
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(.horizontal, 16)
                            .padding(.vertical, 10)
                            .contentShape(Rectangle())
                        }
                        .buttonStyle(.plain)
                        if cp.id != appState.customPlatforms.last?.id {
                            Divider().padding(.leading, 40)
                        }
                    }
                }
            }
            
            Divider()
            
            HStack {
                Button("取消") { isPresented = false }
                Spacer()
                Button("移动") {
                    guard let platformID = selectedPlatformID else { return }
                    let ids = itemIDs.isEmpty ? (itemID.map { [$0] } ?? []) : itemIDs
                    for id in ids {
                        try? itemService.moveToCustomPlatform(itemID: id, customPlatformID: platformID)
                    }
                    isPresented = false
                    appState.refreshData()
                    onMoved?()
                }
                .buttonStyle(.borderedProminent)
                .disabled(selectedPlatformID == nil)
            }
            .padding()
        }
        .frame(width: 320, height: 340)
        .onAppear {
            appState.customPlatforms = (try? appState.customPlatformRepo.fetchAll()) ?? []
            if let singleID = itemID {
                item = try? appState.itemRepo.find(id: singleID)
                selectedPlatformID = item?.customPlatformID
            } else if let firstID = itemIDs.first {
                item = try? appState.itemRepo.find(id: firstID)
                selectedPlatformID = item?.customPlatformID
            }
        }
    }
}
