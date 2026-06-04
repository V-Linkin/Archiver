import SwiftUI

struct NewFolderSheet: View {
    let platform: Platform
    var customPlatformID: UUID? = nil
    var parentID: UUID? = nil
    @Binding var isPresented: Bool
    var onCreate: (() -> Void)?
    @Environment(AppState.self) private var appState
    private let folderService = FolderService()
    @State private var folderName = ""
    
    var body: some View {
        VStack(spacing: 20) {
            Text(parentID != nil ? "新建子文件夹" : "新建文件夹").font(.headline)
            TextField("文件夹名称", text: $folderName).textFieldStyle(.roundedBorder)
            HStack {
                Button("取消") { isPresented = false }.keyboardShortcut(.cancelAction)
                Spacer()
                Button("创建") {
                    try? folderService.createFolder(
                        name: folderName,
                        platform: platform,
                        parentID: parentID,
                        customPlatformID: customPlatformID
                    )
                    isPresented = false
                    appState.refreshData()
                    onCreate?()
                }
                .buttonStyle(.borderedProminent)
                .disabled(folderName.trimmingCharacters(in: .whitespaces).isEmpty)
                .keyboardShortcut(.defaultAction)
            }
        }
        .padding(24)
        .frame(width: 350)
    }
}
