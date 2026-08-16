// ColorLegendView.swift
// Color legend explaining Movie Magic Scheduling strip colors in CineSched.

import SwiftUI

struct ColorLegendView: View {
    @ObservedObject private var l10n = LocalizationManager.shared
    @Environment(\.dismiss) private var dismiss

    struct LegendItem: Identifiable {
        let id = UUID()
        let color: Color
        let title: String
        let description: String
    }

    private var legendItems: [LegendItem] {
        [
            LegendItem(
                color: Color(white: 0.95),
                title: L("INT. DAY (Interior Day)"),
                description: L("Day scenes taking place inside a building, room, or vehicle.")
            ),
            LegendItem(
                color: Color(red: 1.0, green: 0.92, blue: 0.50),
                title: L("EXT. DAY (Exterior Day)"),
                description: L("Day scenes taking place outdoors under sunlight.")
            ),
            LegendItem(
                color: Color(red: 0.60, green: 0.88, blue: 0.65),
                title: L("INT. NIGHT (Interior Night)"),
                description: L("Night scenes taking place inside a building or room.")
            ),
            LegendItem(
                color: Color(red: 0.60, green: 0.80, blue: 0.98),
                title: L("EXT. NIGHT (Exterior Night)"),
                description: L("Night scenes taking place outside in the dark.")
            ),
            LegendItem(
                color: Color(red: 1.0, green: 0.80, blue: 0.60),
                title: L("AFTERNOON / TARDE"),
                description: L("Scenes during afternoon / golden hour.")
            ),
            LegendItem(
                color: Color(red: 0.95, green: 0.85, blue: 0.90),
                title: L("DAWN / DUSK (Amanecer / Atardecer)"),
                description: L("Magic hour scenes during sunrise or sunset.")
            ),
            LegendItem(
                color: Color(white: 0.75),
                title: L("CUSTOM / NOTICE (Aviso / Personalizado)"),
                description: L("Company moves, meal breaks, holidays, or special non-scene strips.")
            )
        ]
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Label(L("Color Legend"), systemImage: "paintpalette.fill")
                    .font(.headline)
                Spacer()
                Button {
                    dismiss()
                } label: {
                    Image(systemName: "xmark.circle.fill")
                        .foregroundColor(.secondary)
                }
                .buttonStyle(.plain)
            }

            Text(L("Standard Movie Magic Scheduling color code used across the Stripboard, Calendar, and Boneyard."))
                .font(.caption)
                .foregroundColor(.secondary)

            Divider()

            VStack(spacing: 8) {
                ForEach(legendItems) { item in
                    HStack(spacing: 10) {
                        RoundedRectangle(cornerRadius: 3)
                            .fill(item.color)
                            .frame(width: 28, height: 20)
                            .overlay(
                                RoundedRectangle(cornerRadius: 3)
                                    .stroke(Color.black.opacity(0.2), lineWidth: 0.5)
                            )

                        VStack(alignment: .leading, spacing: 1) {
                            Text(item.title)
                                .font(.system(size: 11, weight: .bold))
                            Text(item.description)
                                .font(.system(size: 10))
                                .foregroundColor(.secondary)
                        }
                        Spacer()
                    }
                    .padding(5)
                    .background(Color.gray.opacity(0.06))
                    .cornerRadius(5)
                }
            }
        }
        .padding(16)
        .frame(width: 380)
    }
}
