import SwiftUI
import AppKit

class PassthroughLabel: NSTextField {
    override func hitTest(_ point: NSPoint) -> NSView? {
        return nil
    }
    override var acceptsFirstResponder: Bool { false }
}

struct PlaceholderTextEditor: NSViewRepresentable {
    @Binding var text: String
    let placeholder: String
    
    func makeNSView(context: Context) -> PlaceholderTextEditorNSView {
        let view = PlaceholderTextEditorNSView(placeholder: placeholder)
        view.text = text
        view.onTextChange = { newText in
            DispatchQueue.main.async {
                self.text = newText
            }
        }
        return view
    }
    
    func updateNSView(_ nsView: PlaceholderTextEditorNSView, context: Context) {
        if nsView.textView.string != text {
            nsView.textView.string = text
        }
        nsView.updatePlaceholder()
    }
}

// MARK: - 可穿透的 NSScrollView

class PassthroughScrollView: NSScrollView {
    override func scrollWheel(with event: NSEvent) {
        let canScrollVertically = documentView.map { $0.bounds.height > frame.height } ?? false
        guard canScrollVertically else {
            nextResponder?.scrollWheel(with: event)
            return
        }
        let atBottom = (documentView!.bounds.height - (documentVisibleRect.origin.y + documentVisibleRect.height)) < 1.0
        let atTop = documentVisibleRect.origin.y < 1.0
        let scrollingDown = event.scrollingDeltaY > 0
        if (scrollingDown && atBottom) || (!scrollingDown && atTop) {
            nextResponder?.scrollWheel(with: event)
        } else {
            super.scrollWheel(with: event)
        }
    }
}

// MARK: - 支持输入法的 NSTextView 子类

class PlaceholderTextView: NSTextView {
    var onMarkedTextChange: ((Bool) -> Void)?
    
    override func setMarkedText(_ string: Any, selectedRange: NSRange, replacementRange: NSRange) {
        super.setMarkedText(string, selectedRange: selectedRange, replacementRange: replacementRange)
        let hasMarked = (string as? String)?.isEmpty == false
        onMarkedTextChange?(hasMarked)
    }
    
    override func unmarkText() {
        super.unmarkText()
        onMarkedTextChange?(false)
    }
}

class PlaceholderTextEditorNSView: NSView, NSTextViewDelegate {
    var text: String = ""
    var onTextChange: ((String) -> Void)?
    
    let textView = PlaceholderTextView()
    var placeholderLabel: PassthroughLabel!
    var scrollView: NSScrollView!
    
    init(placeholder: String) {
        super.init(frame: .zero)
        setup(placeholder: placeholder)
    }
    
    required init?(coder: NSCoder) {
        super.init(coder: coder)
        setup(placeholder: "")
    }
    
    private func setup(placeholder: String) {
        textView.font = NSFont.systemFont(ofSize: NSFont.systemFontSize)
        textView.textColor = NSColor.labelColor
        textView.isRichText = false
        textView.allowsUndo = true
        textView.textContainerInset = NSSize(width: 5, height: 8)
        textView.backgroundColor = .clear
        textView.drawsBackground = false
        textView.delegate = self
        
        // 输入法组合文字变化时立即隐藏/显示占位符
        textView.onMarkedTextChange = { [weak self] hasMarked in
            guard let self = self else { return }
            if hasMarked {
                self.placeholderLabel.isHidden = true
            } else {
                self.updatePlaceholder()
            }
        }
        
        scrollView = PassthroughScrollView()
        scrollView.documentView = textView
        scrollView.hasVerticalScroller = true
        scrollView.hasHorizontalScroller = false
        scrollView.autohidesScrollers = true
        scrollView.drawsBackground = false
        scrollView.borderType = .noBorder
        scrollView.translatesAutoresizingMaskIntoConstraints = false
        
        addSubview(scrollView)
        NSLayoutConstraint.activate([
            scrollView.leadingAnchor.constraint(equalTo: leadingAnchor),
            scrollView.trailingAnchor.constraint(equalTo: trailingAnchor),
            scrollView.topAnchor.constraint(equalTo: topAnchor),
            scrollView.bottomAnchor.constraint(equalTo: bottomAnchor)
        ])
        
        placeholderLabel = PassthroughLabel(labelWithString: placeholder)
        placeholderLabel.font = NSFont.systemFont(ofSize: NSFont.systemFontSize)
        placeholderLabel.textColor = .placeholderTextColor
        placeholderLabel.translatesAutoresizingMaskIntoConstraints = false
        addSubview(placeholderLabel)
        
        NSLayoutConstraint.activate([
            placeholderLabel.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 11),
            placeholderLabel.topAnchor.constraint(equalTo: topAnchor, constant: 7)
        ])
    }
    
    func updatePlaceholder() {
        placeholderLabel.isHidden = !textView.string.isEmpty
    }
    
    func textDidChange(_ notification: Notification) {
        updatePlaceholder()
        onTextChange?(textView.string)
    }
}
