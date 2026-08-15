// NewSceneInputView.swift
// Form for creating a new Scene in the Boneyard sidebar

import SwiftUI

struct NewSceneInputView: View {
    @Binding var newSceneNumber: String
    @Binding var newSceneTitle: String
    @Binding var newDuration:   String
    @Binding var newEstimate:   String
    @Binding var newDayNightType: DayNightType
    @Binding var allScenes:     [Scene]

    @State private var newRealLocation:      String = ""
    @State private var durationIsValid:      Bool = true
    @State private var estimatedTimeIsValid: Bool = true

    var knownLocations: [String] {
        Array(Set(allScenes.map { $0.realLocation }.filter { !$0.isEmpty })).sorted()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 6) {
                TextField("#", text: $newSceneNumber)
                    .frame(width: 60)
                    .help("Scene number (optional)")
                TextField("Scene Title", text: $newSceneTitle)
            }

            // Real Location with Autocomplete
            LocationAutocompleteField(
                title: "Real Location / Set",
                placeholder: "e.g. Playa de la Concha",
                text: $newRealLocation,
                suggestions: knownLocations
            )

            // Duration field — optional for Custom strips / notice strips
            VStack(alignment: .leading, spacing: 4) {
                TextField("Page Count (e.g. 1 4/8)", text: $newDuration)
                    .onChange(of: newDuration) { _ in validateInputs() }

                if !durationIsValid {
                    Text("Invalid page format. Use: 15, 1 7/8, 7/8")
                        .font(.caption).foregroundColor(.red)
                } else if let eighths = FractionParser.parseToEighths(newDuration), !newDuration.isEmpty {
                    Text("= \(FractionParser.formatEighths(eighths)) pages")
                        .font(.caption).foregroundColor(.secondary)
                } else if newDuration.isEmpty {
                    Text("Leave blank for no page count")
                        .font(.caption).foregroundColor(.secondary)
                }
            }

            // Estimated time field
            VStack(alignment: .leading, spacing: 4) {
                TextField("Est. Shoot Time (e.g. 4, 15, 2:30)", text: $newEstimate)
                    .onChange(of: newEstimate) { _ in validateInputs() }

                if !estimatedTimeIsValid {
                    Text("Invalid time format. Use: 4 (hours), 15 (mins), 2:30")
                        .font(.caption).foregroundColor(.red)
                } else if let hint = TimeParser.getInputHint(newEstimate), !newEstimate.isEmpty {
                    Text(hint).font(.caption).foregroundColor(.secondary)
                } else if newEstimate.isEmpty {
                    Text("Leave blank for no time estimate")
                        .font(.caption).foregroundColor(.secondary)
                }
            }

            // Day / Night / Dawn / Dusk / Afternoon / Custom toggle
            VStack(alignment: .leading, spacing: 4) {
                Text("Time:").font(.caption).foregroundColor(.secondary)

                LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible()), GridItem(.flexible())], spacing: 6) {
                    ForEach(DayNightType.allCases, id: \.self) { type in
                        Button {
                            newDayNightType = type
                        } label: {
                            HStack(spacing: 4) {
                                Image(systemName: newDayNightType == type ? "checkmark.circle.fill" : "circle")
                                    .foregroundColor(type.color)
                                    .font(.caption)
                                Text(type == .custom ? "Custom" : type.displayName)
                                    .font(.caption)
                                    .foregroundColor(newDayNightType == type ? type.color : .primary)
                                    .lineLimit(1)
                                    .fixedSize(horizontal: true, vertical: false)
                            }
                        }
                        .buttonStyle(.plain)
                    }
                }
            }

            Button("Add Scene") {
                addScene()
            }
            .disabled(!canAddScene())
        }
        .padding(8)
    }

    private func validateInputs() {
        durationIsValid      = FractionParser.parseToEighths(newDuration) != nil || newDuration.isEmpty
        estimatedTimeIsValid = TimeParser.parseToMinutes(newEstimate) != nil || newEstimate.isEmpty
    }

    /// A title is the only thing a scene actually needs — duration and time
    /// are optional and default to zero when left blank, since a
    /// notice strip (no scene number, e.g. "DOWN FOR THANKSGIVING") often has
    /// neither. Non-empty text in either field still has to actually parse.
    private func canAddScene() -> Bool {
        let titleOK = !newSceneTitle.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        return titleOK && durationIsValid && estimatedTimeIsValid
    }

    private func addScene() {
        guard canAddScene() else { return }

        let duration = FractionParser.parseToEighths(newDuration) ?? 0
        let estimate = TimeParser.parseToMinutes(newEstimate) ?? 0

        allScenes.append(Scene(
            title:         newSceneTitle,
            sceneNumber:   newSceneNumber.trimmingCharacters(in: .whitespaces),
            duration:      duration,
            estimatedTime: estimate,
            dayNightType:  newDayNightType,
            realLocation:  newRealLocation.trimmingCharacters(in: .whitespaces)
        ))

        newSceneNumber  = ""
        newSceneTitle   = ""
        newRealLocation = ""
        newDuration     = ""
        newEstimate     = ""
        newDayNightType = .day
    }
}
