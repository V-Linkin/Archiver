import SwiftUI

struct MoveToFolderOverlay: View {
    let itemID: UUID
    @Binding var isPresented: Bool
    @Environment(AppState.self) private var appState
    private let itemService = ItemService()
    @State private var folders: [Folder] = []
    @State private var selectedFolderID: UUID? = nil
    
    var body: some View {
        VStack(spacing: 0) {
            Text("移动到文件夹")
                .font(.headline)
                .padding()
            
            Divider()
            
            if folders.isEmpty {
                VStack(spacing: 12) {
                    Spacer()
                    Image(systemName: "folder").font(.largeTitle).foregroundStyle(.tertiary)
                    Text("暂无文件夹").foregroundStyle(.secondary)
                        .font(.subheadline)
                    Spacer()
                }
            } else {
                ScrollView {
                    VStack(spacing: 0) {
                        ForEach(Array(folders), id: \.id) { folder in
                            Button {
                                selectedFolderID = folder.id
                            } label: {
                                HStack {
                                    Image(systemName: selectedFolderID == folder.id ? "folder.fill" : "folder")
                                        .foregroundStyle(.blue)
                                    Text(folderPlatformName(folder))
                                    Spacer()
                                    if selectedFolderID == folder.id {
                                        Image(systemName: "checkmark").foregroundStyle(.blue)
                                    }
                                }
                                .padding(.horizontal, 16)
                                .padding(.vertical, 10)
                            }
                            .buttonStyle(.plain)
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
                    guard let folderID = selectedFolderID else { return }
                    try? itemService.moveToFolder(itemID: itemID, folderID: folderID)
                    isPresented = false
                    appState.refreshData()
                }
                .buttonStyle(.borderedProminent)
                .disabled(selectedFolderID == nil)
            }
            .padding()
        }
        .frame(width: 320, height: 300)
        .background(.background)
        .clipShape(RoundedRectangle(cornerRadius: 12))
        .shadow(radius: 20)
        .onAppear {
            folders = (try? appState.folderRepo.fetchAll(platform: .custom)) ?? []
            if let item = try? appState.itemRepo.find(id: itemID) {
                selectedFolderID = item.folderID
            }
        }
    }
    
    private func folderPlatformName(_ folder: Folder) -> String {
        if let cpID = folder.customPlatformID,
           let cp = try? appState.customPlatformRepo.find(id: cpID) {
            return "\(cp.name) - \(folder.name)"
        }
        return folder.name
    }
}
