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
                color: Color(hex: "F3F4F6"),
                title: L("INT. DAY (Interior Day / Interior Día)"),
                description: L("Day scenes taking place inside a building, room, or vehicle.")
            ),
            LegendItem(
                color: Color(hex: "FEF08A"),
                title: L("EXT. DAY (Exterior Day / Exterior Día)"),
                description: L("Day scenes taking place outdoors under sunlight.")
            ),
            LegendItem(
                color: Color(hex: "86EFAC"),
                title: L("INT. NIGHT (Interior Night / Interior Noche)"),
                description: L("Night scenes taking place inside a building or room.")
            ),
            LegendItem(
                color: Color(hex: "93C5FD"),
                title: L("EXT. NIGHT (Exterior Night / Exterior Noche)"),
                description: L("Night scenes taking place outside in the dark.")
            ),
            LegendItem(
                color: Color(hex: "FFE4C4"),
                title: L("INT. TARDE (Interior Afternoon)"),
                description: L("Afternoon scenes taking place indoors.")
            ),
            LegendItem(
                color: Color(hex: "FDBA74"),
                title: L("EXT. TARDE (Exterior Afternoon)"),
                description: L("Afternoon scenes outdoors during golden hour.")
            ),
            LegendItem(
                color: Color(hex: "FDE68A"),
                title: L("AMANECER / DAWN (Alba)"),
                description: L("Magic hour scenes taking place during sunrise.")
            ),
            LegendItem(
                color: Color(hex: "E9D5FF"),
                title: L("ATARDECER / DUSK (Ocaso)"),
                description: L("Magic hour scenes taking place during sunset.")
            ),
            LegendItem(
                color: Color(hex: "D1D5DB"),
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

            ScrollView {
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
                        .padding(6)
                        .background(Color.gray.opacity(0.06))
                        .cornerRadius(5)
                    }
                }
            }

            Divider()

            HStack {
                Spacer()
                Button(L("Close")) { dismiss() }
                    .buttonStyle(.borderedProminent)
            }
        }
        .padding(18)
        .frame(width: 440, height: 500)
    }
}
