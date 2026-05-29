import SwiftUI

struct TargetPickerBar: View {
    @Binding var target: FileKind?
    let options: [FileKind]

    var body: some View {
        HStack(spacing: 8) {
            Text("Convert to")
                .font(.subheadline.weight(.medium))
                .foregroundStyle(.secondary)
            Picker("", selection: $target) {
                if options.isEmpty {
                    Text("No common target").tag(FileKind?.none)
                } else {
                    ForEach(grouped, id: \.0) { category, kinds in
                        Section(header: Text(category.displayName)) {
                            ForEach(kinds) { kind in
                                Text(kind.displayName).tag(FileKind?.some(kind))
                            }
                        }
                    }
                }
            }
            .labelsHidden()
            .pickerStyle(.menu)
            .disabled(options.isEmpty)
            .fixedSize()
        }
    }

    private var grouped: [(FileCategory, [FileKind])] {
        let buckets = Dictionary(grouping: options, by: \.category)
        return FileCategory.allCases.compactMap { cat in
            guard let kinds = buckets[cat], !kinds.isEmpty else { return nil }
            return (cat, kinds.sorted { $0.displayName < $1.displayName })
        }
    }
}
