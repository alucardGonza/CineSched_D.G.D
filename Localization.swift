// Localization.swift
// Multilingual support (English / Español) for CineSched UI, menus, and Call Sheet exports.

import SwiftUI

enum AppLanguage: String, CaseIterable, Codable {
    case english = "en"
    case spanish = "es"

    var displayName: String {
        switch self {
        case .english: return "English"
        case .spanish: return "Español"
        }
    }
}

class LocalizationManager: ObservableObject {
    static let shared = LocalizationManager()

    @AppStorage("cinesched_app_language") var currentLanguage: AppLanguage = .english {
        didSet {
            objectWillChange.send()
        }
    }

    func setLanguage(_ lang: AppLanguage) {
        currentLanguage = lang
    }
}

// Global helper function for translations
func L(_ key: String, lang: AppLanguage = LocalizationManager.shared.currentLanguage) -> String {
    let dict: [String: [AppLanguage: String]] = [
        // Top Menus / Toolbar
        "View": [.english: "View", .spanish: "Ver"],
        "Language": [.english: "Language", .spanish: "Idioma"],
        "Production Setup": [.english: "Production Setup", .spanish: "Configuración de Producción"],
        "Call Sheet": [.english: "Call Sheet", .spanish: "Orden de Rodaje"],
        "Export PDF": [.english: "Export PDF", .spanish: "Exportar PDF"],
        "Save": [.english: "Save", .spanish: "Guardar"],
        "Cancel": [.english: "Cancel", .spanish: "Cancelar"],

        // Call Sheet Editor Tabs
        "General & Schedule": [.english: "General & Schedule", .spanish: "General y Horarios"],
        "Weather": [.english: "Weather", .spanish: "Clima"],
        "Locations": [.english: "Locations", .spanish: "Locaciones"],
        "Cast": [.english: "Cast", .spanish: "Actores (Cast)"],
        "Crew": [.english: "Crew", .spanish: "Equipo Técnico"],
        "General Notes": [.english: "General Notes", .spanish: "Observaciones Generales"],

        // Call Sheet PDF Terms
        "CALL SHEET": [.english: "CALL SHEET", .spanish: "ORDEN DE RODAJE"],
        "GENERAL CALL": [.english: "GENERAL CALL", .spanish: "CITACIÓN GENERAL"],
        "SHOOTING CONTACTS": [.english: "SHOOTING CONTACTS", .spanish: "CONTACTOS EN RODAJE"],
        "PRODUCER": [.english: "PRODUCER", .spanish: "PRODUCTOR"],
        "DIRECTOR": [.english: "DIRECTOR", .spanish: "DIRECTOR"],
        "1ST AD": [.english: "1ST AD", .spanish: "AYUDANTE DE DIRECCIÓN"],
        "READY TO SHOOT": [.english: "READY TO SHOOT", .spanish: "LISTOS"],
        "LUNCH": [.english: "LUNCH", .spanish: "ALMUERZO"],
        "SNACK": [.english: "SNACK", .spanish: "MERIENDA"],
        "WRAP": [.english: "WRAP", .spanish: "FIN / CENA"],
        "WEATHER FORECAST": [.english: "WEATHER FORECAST", .spanish: "PREVISIÓN METEOROLÓGICA"],
        "NEAREST HOSPITAL": [.english: "NEAREST HOSPITAL", .spanish: "HOSPITAL MÁS CERCANO"],
        "SCENE": [.english: "SCENE", .spanish: "ESCENA"],
        "SET / DESCRIPTION": [.english: "SET / DESCRIPTION", .spanish: "DECORADO / SINOPSIS"],
        "PAGES": [.english: "PAGES", .spanish: "PÁGINAS"],
        "LOC": [.english: "LOC", .spanish: "LOC"],
        "ADDRESS": [.english: "ADDRESS", .spanish: "DIRECCIÓN"],
        "CHARACTER": [.english: "CHARACTER", .spanish: "PERSONAJE"],
        "ACTOR/ACTRESS": [.english: "ACTOR/ACTRESS", .spanish: "ACTOR/ACTRIZ"],
        "STATUS": [.english: "STATUS", .spanish: "ECDT"],
        "PICK UP": [.english: "PICK UP", .spanish: "RECOGIDA"],
        "H/MU & WARDROBE": [.english: "H/MU & WARDROBE", .spanish: "VEST. Y MAQ."],
        "ON SET": [.english: "ON SET", .spanish: "LISTOS"],
        "CREW CALL TIMES": [.english: "CREW CALL TIMES", .spanish: "CITACIÓN ESPECÍFICA DEL EQUIPO TÉCNICO"],
        "Quote of the day": [.english: "Quote of the day", .spanish: "Frase del día"],

        // Scene Breakdown
        "Real Location": [.english: "Real Location (Set)", .spanish: "Locación Real (Set)"],
        "Scene #": [.english: "Scene #", .spanish: "Escena #"],
        "Scene Title": [.english: "Scene Title", .spanish: "Título de Escena"],
        "Duration (pages)": [.english: "Duration (pages)", .spanish: "Duración (páginas)"],
        "Estimated Time": [.english: "Estimated Time (min)", .spanish: "Tiempo Estimado (min)"],
        "Scene Summary": [.english: "Scene Summary", .spanish: "Sinopsis de Escena"]
    ]

    return dict[key]?[lang] ?? key
}
