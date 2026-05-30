# Image Viewer Design

## Overview

Replace the current simple fullscreen overlay (single image, tap to dismiss) with a full-featured image viewer that supports zoom, pan, and image navigation. The viewer is invoked from two separate contexts: cover/media images and body text images, each maintaining its own independent image gallery.

## Current State

- `ContentView.swift` contains a `zoomedImage: NSImage?` state and a simple overlay at line 186
- `ItemDetailView.swift` passes the tapped image via `zoomedImage` binding
- No zoom, pan, or navigation capabilities exist
- Images in body text are rendered by `MarkdownView.swift` → `AsyncImageView`

## Design

### New Component: `ImageViewerView.swift`

Location: `Views/Components/ImageViewerView.swift`

```swift
struct ImageViewerView: View {
    let images: [NSImage]
    @Binding var currentIndex: Int
    @Binding var isPresented: Bool
}
```

### Zoom & Pan

- Use `MagnificationGesture` for pinch/scroll zoom, tracked via `@State scale: CGFloat = 1.0`
- Use `DragGesture` for panning, tracked via `@State offset: CGSize = .zero`
- Double-tap resets `scale` to 1.0 and `offset` to `.zero` with animation
- On macOS: mouse scroll wheel controls zoom, drag to pan

### Image Navigation

- Left/right arrow buttons appear on hover at the screen edges, styled as semi-transparent circles with chevron icons
- Keyboard left/right arrow keys navigate between images
- First image: left arrow disabled (hidden or grayed out)
- Last image: right arrow disabled (hidden or grayed out)
- Navigation resets zoom/pan state to defaults
- Bottom center: page indicator text "current / total" (e.g., "3 / 12")

### Dismiss

- ESC key closes the viewer
- Click on the black background area (not on the image) closes the viewer
- Closing resets zoom/pan state

### State Management

**ItemDetailView changes:**

- Add `@State coverImages: [NSImage]` and `@State coverImageIndex: Int`
- Add `@State bodyImages: [NSImage]` and `@State bodyImageIndex: Int`
- Add `@State showCoverViewer: Bool` and `@State showBodyViewer: Bool`
- When a cover/media image is tapped: populate `coverImages` from `mediaAssets`, set `coverImageIndex`, set `showCoverViewer = true`
- When a body image is tapped: populate `bodyImages` from all body image URLs (resolved to NSImage), set `bodyImageIndex`, set `showBodyViewer = true`

**Body image collection**: Parse `item.body` markdown text using the same `![alt](url)` regex pattern from `MarkdownView.parseBlocks()`. Extract all image URLs. On viewer open, preload all images as `[NSImage]`. This ensures the gallery knows all images even if only one was tapped.

**ContentView changes:**

- Remove `zoomedImage: NSImage?` state
- Remove the existing simple overlay
- Add two `ImageViewerView` instances: one for cover images, one for body images
- Both bound to ItemDetailView's state via bindings

### Two Separate Image Spaces

- **Cover gallery**: Contains only the media/cover images from the top of the detail view. Swiping only cycles through these images.
- **Body gallery**: Contains only the inline images from the article body text (parsed from MarkdownView). Swiping only cycles through these images.
- Each gallery is independent; they do not share navigation state.

## Files Modified

| File | Change |
|------|--------|
| `Views/Components/ImageViewerView.swift` | New file — full viewer component |
| `Views/Item/ItemDetailView.swift` | Add gallery state, pass to ImageViewerView |
| `App/ContentView.swift` | Remove old overlay, add two ImageViewerView instances |

## Testing

- Open a detail view with both cover and body images
- Verify cover image viewer opens on cover tap, body viewer opens on body image tap
- Verify zoom in/out via scroll wheel, drag to pan, double-tap to reset
- Verify left/right navigation with arrow keys and on-screen buttons
- Verify first/last image edge cases (no prev on first, no next on last)
- Verify ESC and background tap dismiss the viewer
- Verify zoom state resets on image switch and dismiss
