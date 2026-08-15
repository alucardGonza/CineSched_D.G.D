// Models.swift
// Core data models for CineSched

import SwiftUI
import AppKit

// MARK: - Color blending

extension Color {
    /// Blends this color toward white by `amount` (0...1) — e.g. 0.1 mixes in
    /// 10% white, keeping 90% of the original. Computed from this color's
    /// actual RGB components rather than a guessed replacement value, so it
    /// tracks whatever the base color really is.
    func lightened(by amount: Double) -> Color {
        let ns = NSColor(self).usingColorSpace(.deviceRGB) ?? NSColor(self)
        let r = ns.redComponent, g = ns.greenComponent, b = ns.blueComponent, a = ns.alphaComponent
        return Color(
            red:   r + (1 - r) * amount,
            green: g + (1 - g) * amount,
            blue:  b + (1 - b) * amount,
            opacity: a
        )
    }
}

// MARK: - DayNightType

enum DayNightType: String, Codable, CaseIterable {
    case day       = "DAY"
    case night     = "NIGHT"
    case dawn      = "DAWN"
    case dusk      = "DUSK"
    case afternoon = "AFTERNOON"
    case custom    = "CUSTOM"

    var color: Color {
        switch self {
        case .day:       return Color.orange
        case .night:     return Color.blue
        case .dawn:      return Color(red: 0.96, green: 0.72, blue: 0.18)
        case .dusk:      return Color(red: 0.65, green: 0.35, blue: 0.85)
        case .afternoon: return Color(red: 0.90, green: 0.25, blue: 0.40)
        case .custom:    return Color.green
        }
    }

    var displayName: String { rawValue }

    var shortCode: String {
        switch self {
        case .day:       return "D"
        case .night:     return "N"
        case .dawn:      return "DW"
        case .dusk:      return "DK"
        case .afternoon: return "AFT"
        case .custom:    return "C"
        }
    }

    var sortOrder: Int {
        switch self {
        case .day:       return 0
        case .dawn:      return 1
        case .afternoon: return 2
        case .dusk:      return 3
        case .night:     return 4
        case .custom:    return 5
        }
    }
}

// MARK: - Scene

struct Scene: Identifiable, Codable, Hashable {
    let id: UUID
    var title: String
    var sceneNumber: String
    var duration: Int
    var estimatedTime: Int
    var dayNightType: DayNightType
    var cast: [String]
    var summary: String
    // Breakdown tagging — set via SceneEditSheet's Breakdown section or the Breakdown
    // Browser, printed one page per scene via BreakdownExporter.
    var realLocation: String
    var locationAddress: String
    var extras: [String]
    var props: [String]
    var setDressing: [String]
    var wardrobe: [String]
    var makeupHair: [String]
    var vehicles: [String]
    var specialEquipment: [String]
    var stunts: [String]
    var sfx: [String]
    var vfx: [String]
    var breakdownNotes: String

    init(
        title: String,
        sceneNumber: String = "",
        duration: Int,
        estimatedTime: Int,
        dayNightType: DayNightType = .day,
        cast: [String] = [],
        summary: String = "",
        realLocation: String = "",
        locationAddress: String = "",
        extras: [String] = [],
        props: [String] = [],
        setDressing: [String] = [],
        wardrobe: [String] = [],
        makeupHair: [String] = [],
        vehicles: [String] = [],
        specialEquipment: [String] = [],
        stunts: [String] = [],
        sfx: [String] = [],
        vfx: [String] = [],
        breakdownNotes: String = ""
    ) {
        self.id               = UUID()
        self.title            = title
        self.sceneNumber      = sceneNumber
        self.duration         = duration
        self.estimatedTime    = estimatedTime
        self.dayNightType     = dayNightType
        self.cast             = cast
        self.summary          = summary
        self.realLocation     = realLocation
        self.locationAddress  = locationAddress
        self.extras           = extras
        self.props            = props
        self.setDressing      = setDressing
        self.wardrobe         = wardrobe
        self.makeupHair       = makeupHair
        self.vehicles         = vehicles
        self.specialEquipment = specialEquipment
        self.stunts           = stunts
        self.sfx              = sfx
        self.vfx              = vfx
        self.breakdownNotes   = breakdownNotes
    }

    enum CodingKeys: String, CodingKey {
        case id, title, sceneNumber, duration, estimatedTime, dayNightType, cast, summary
        case realLocation, locationAddress
        case extras, props, setDressing, wardrobe, makeupHair, vehicles, specialEquipment, stunts, sfx, vfx, breakdownNotes
    }

    init(from decoder: Decoder) throws {
        let c         = try decoder.container(keyedBy: CodingKeys.self)
        id            = try c.decode(UUID.self,         forKey: .id)
        title         = try c.decode(String.self,       forKey: .title)
        sceneNumber   = try c.decodeIfPresent(String.self, forKey: .sceneNumber) ?? ""
        duration      = try c.decode(Int.self,          forKey: .duration)
        estimatedTime = try c.decode(Int.self,          forKey: .estimatedTime)
        dayNightType  = try c.decode(DayNightType.self, forKey: .dayNightType)
        summary       = try c.decodeIfPresent(String.self, forKey: .summary) ?? ""
        realLocation  = try c.decodeIfPresent(String.self, forKey: .realLocation) ?? ""
        locationAddress = try c.decodeIfPresent(String.self, forKey: .locationAddress) ?? ""
        if let array = try? c.decode([String].self, forKey: .cast) {
            cast = array
        } else if let legacy = try? c.decode(String.self, forKey: .cast) {
            cast = legacy.components(separatedBy: ",")
                .map { $0.trimmingCharacters(in: .whitespaces) }
                .filter { !$0.isEmpty }
        } else {
            cast = []
        }
        extras           = try c.decodeIfPresent([String].self, forKey: .extras) ?? []
        props            = try c.decodeIfPresent([String].self, forKey: .props) ?? []
        setDressing      = try c.decodeIfPresent([String].self, forKey: .setDressing) ?? []
        wardrobe         = try c.decodeIfPresent([String].self, forKey: .wardrobe) ?? []
        makeupHair       = try c.decodeIfPresent([String].self, forKey: .makeupHair) ?? []
        vehicles         = try c.decodeIfPresent([String].self, forKey: .vehicles) ?? []
        specialEquipment = try c.decodeIfPresent([String].self, forKey: .specialEquipment) ?? []
        stunts           = try c.decodeIfPresent([String].self, forKey: .stunts) ?? []
        sfx              = try c.decodeIfPresent([String].self, forKey: .sfx) ?? []
        vfx              = try c.decodeIfPresent([String].self, forKey: .vfx) ?? []
        breakdownNotes   = try c.decodeIfPresent(String.self, forKey: .breakdownNotes) ?? ""
    }

    /// "12A. INT. HOUSE - DAY" for display — combines the dedicated number field
    /// with the title. Falls back to the bare title when there's no number, which
    /// also covers scenes saved before this field existed (their number, if any,
    /// is already part of `title` from that era).
    var displayTitle: String {
        let trimmedNum = sceneNumber.trimmingCharacters(in: .whitespaces)
        return trimmedNum.isEmpty ? title : "\(trimmedNum). \(title)"
    }

    /// Extracted scene number: uses dedicated sceneNumber or parses leading digits from title (e.g. "2. EXT..." -> "2")
    var extractedSceneNumber: String {
        let trimmed = sceneNumber.trimmingCharacters(in: .whitespaces)
        if !trimmed.isEmpty { return trimmed }
        let titleTrimmed = title.trimmingCharacters(in: .whitespaces)
        if let match = titleTrimmed.range(of: "^#?(\\d+[A-Za-z]?)", options: .regularExpression) {
            return String(titleTrimmed[match]).replacingOccurrences(of: "#", with: "")
        }
        return "1"
    }

    /// Extracted decorado/set name: removes leading scene number, INT/EXT, and trailing time of day
    var decoradoOnly: String {
        if !realLocation.trimmingCharacters(in: .whitespaces).isEmpty {
            return realLocation.trimmingCharacters(in: .whitespaces).uppercased()
        }
        var s = title.trimmingCharacters(in: .whitespaces)
        // Strip leading number like "1. ", "12A - ", "#2. "
        s = s.replacingOccurrences(of: "^#?\\d+[A-Za-z]?\\.?\\s*[-–—.]?\\s*", with: "", options: .regularExpression)
        // Strip INT./EXT., INT., EXT., I/E., I/E
        s = s.replacingOccurrences(of: "^(INT\\.?/EXT\\.?|INT\\.?|EXT\\.?|I/E\\.?|INT\\s+/\\s+EXT)\\s*[-–—.]?\\s*", with: "", options: [.regularExpression, .caseInsensitive])
        // Strip trailing time of day: " - DAY", ". DIA", " - NIGHT", " - NOCHE", etc.
        s = s.replacingOccurrences(of: "\\s*[-–—.]+\\s*(DAY|NIGHT|DAWN|DUSK|AFTERNOON|DIA|NOCHE|TARDE|ATARDECER|AMANECER|CONTINUOUS|SAME)\\.?\\s*$", with: "", options: [.regularExpression, .caseInsensitive])
        s = s.trimmingCharacters(in: CharacterSet(charactersIn: " .-–—"))
        return s.isEmpty ? title.uppercased() : s.uppercased()
    }

    // MARK: - Interior / Exterior + Movie Magic strip color

    enum IntExt { case interior, exterior, unknown }

    /// Interior/exterior, sniffed from title even when preceded by scene number
    var intExt: IntExt {
        var upper = title.trimmingCharacters(in: .whitespaces).uppercased()
        upper = upper.replacingOccurrences(of: "^#?\\d+[A-Za-z]?\\.?\\s*", with: "", options: .regularExpression)
        if upper.hasPrefix("EXT.") || upper.hasPrefix("EXT ") || upper.hasPrefix("EXT") {
            return .exterior
        }
        return .interior
    }

    var intExtString: String {
        var upper = title.trimmingCharacters(in: .whitespaces).uppercased()
        upper = upper.replacingOccurrences(of: "^#?\\d+[A-Za-z]?\\.?\\s*", with: "", options: .regularExpression)
        if upper.hasPrefix("INT/EXT") || upper.hasPrefix("INT./EXT") || upper.hasPrefix("I/E") || upper.hasPrefix("INT / EXT") {
            return "INT/EXT"
        }
        if upper.hasPrefix("EXT.") || upper.hasPrefix("EXT ") || upper.hasPrefix("EXT") {
            return "EXT"
        }
        return "INT"
    }

    /// Movie Magic Scheduling's own strip color code:
    /// white = day interior, yellow = day exterior, green = night interior,
    /// blue = night exterior, rose/gold for dawn/afternoon/dusk. A scene with no scene number is treated as a
    /// notice strip (e.g. "DOWN FOR THANKSGIVING") rather than a real scene —
    /// those go black. Colors are mixed 10% toward white — softer than the
    /// literal paper-strip hues, while still reading as the same colors at a
    /// glance.
    var stripColor: Color {
        let base: Color
        if sceneNumber.trimmingCharacters(in: .whitespaces).isEmpty {
            base = .black
        } else if dayNightType == .custom {
            base = Color(white: 0.75)
        } else {
            switch (intExt, dayNightType) {
            case (.interior, .day):       base = .white
            case (.exterior, .day):       base = .yellow
            case (.interior, .night):     base = .green
            case (.exterior, .night):     base = .blue
            case (.interior, .dawn):      base = Color(red: 0.95, green: 0.85, blue: 0.90)
            case (.exterior, .dawn):      base = Color(red: 1.0,  green: 0.88, blue: 0.70)
            case (.interior, .dusk):      base = Color(red: 0.85, green: 0.80, blue: 0.95)
            case (.exterior, .dusk):      base = Color(red: 0.75, green: 0.70, blue: 0.90)
            case (.interior, .afternoon): base = Color(red: 1.0,  green: 0.92, blue: 0.80)
            case (.exterior, .afternoon): base = Color(red: 1.0,  green: 0.80, blue: 0.60)
            case (.unknown, .day):        base = .white
            case (.unknown, .night):      base = .blue
            case (.unknown, .dawn):       base = Color(red: 0.95, green: 0.85, blue: 0.90)
            case (.unknown, .dusk):       base = Color(red: 0.85, green: 0.80, blue: 0.95)
            case (.unknown, .afternoon):  base = Color(red: 1.0,  green: 0.80, blue: 0.60)
            case (_, .custom):            base = Color(white: 0.75)
            }
        }
        return base.lightened(by: 0.1)
    }

    /// White for the black "no scene number" notice-strip treatment, black
    /// for every normal color-coded strip — keeps text readable against
    /// whichever `stripColor` this scene gets.
    var stripTextColor: Color {
        sceneNumber.trimmingCharacters(in: .whitespaces).isEmpty ? .white : .black
    }

    var isNoticeStrip: Bool {
        sceneNumber.trimmingCharacters(in: .whitespaces).isEmpty
    }

    /// Splits a raw scene number like "12A" into its numeric and letter parts.
    static func parseSceneNumber(_ raw: String) -> (number: Int, letter: String)? {
        let trimmed = raw.trimmingCharacters(in: .whitespaces)
        let digits  = trimmed.prefix { $0.isNumber }
        let letter  = trimmed.dropFirst(digits.count).prefix { $0.isLetter }
        guard let number = Int(digits) else { return nil }
        return (number, String(letter).uppercased())
    }
}

// MARK: - Location

struct Location: Identifiable, Codable, Hashable {
    let id: UUID
    var name:    String
    var address: String

    init(name: String = "", address: String = "") {
        self.id      = UUID()
        self.name    = name
        self.address = address
    }
}

// MARK: - CastCallEntry

struct CastCallEntry: Identifiable, Codable, Hashable {
    let id: UUID
    var characterName: String
    var actorName: String
    var ecdt: String             // "E", "ET", "W", etc.
    var pickupTime: String       // e.g. "7:00"
    var hmuWardrobeTime: String  // e.g. "7:30"
    var onSetTime: String        // e.g. "8:00"
    var wrapTime: String         // e.g. "21:30"
    var locationIndex: String    // e.g. "1", "2"

    init(
        id: UUID = UUID(),
        characterName: String = "",
        actorName: String = "",
        ecdt: String = "E",
        pickupTime: String = "",
        hmuWardrobeTime: String = "",
        onSetTime: String = "",
        wrapTime: String = "",
        locationIndex: String = "1"
    ) {
        self.id = id
        self.characterName = characterName
        self.actorName = actorName
        self.ecdt = ecdt
        self.pickupTime = pickupTime
        self.hmuWardrobeTime = hmuWardrobeTime
        self.onSetTime = onSetTime
        self.wrapTime = wrapTime
        self.locationIndex = locationIndex
    }
}

// MARK: - CrewCallEntry

struct CrewCallEntry: Identifiable, Codable, Hashable {
    let id: UUID
    var role: String
    var name: String
    var callTime: String
    var phone: String

    init(
        id: UUID = UUID(),
        role: String = "",
        name: String = "",
        callTime: String = "",
        phone: String = ""
    ) {
        self.id = id
        self.role = role
        self.name = name
        self.callTime = callTime
        self.phone = phone
    }
}

// MARK: - CallSheetData

struct CallSheetData: Codable {
    var generalCallTime: String
    var workDaySchedule: String       // e.g. "Jornada de 7:30 a 21:30 h"
    var readyToShootTime: String      // e.g. "08:00 AM"
    var lunchTime: String             // e.g. "01:30 PM"
    var snackTime: String             // Merienda, e.g. "05:00 PM"
    var dinnerTime: String            // e.g. "09:30 PM"
    var quoteOfTheDay: String         // "Quote of the day"
    var prodManagerContact: String    // Producer contact override if needed
    var adContact: String             // AD contact
    var weatherTemp: String
    var weatherCondition: String
    var weatherPrecipWind: String
    var sunTimes: String
    var nearestHospital: String
    var castCallEntries: [CastCallEntry]
    var crewCallEntries: [CrewCallEntry]
    var productionNotes: [String]
    var locations:       [Location]
    var castOverride:    [String]?
    var crewOverride:    [String]?
    var crewIDOverride:  [UUID]?
    var crewOneOffs:     [String]?
    var notes:           String

    init(
        generalCallTime: String     = "",
        workDaySchedule: String     = "",
        readyToShootTime: String    = "",
        lunchTime: String           = "",
        snackTime: String           = "",
        dinnerTime: String          = "",
        quoteOfTheDay: String       = "",
        prodManagerContact: String  = "",
        adContact: String           = "",
        weatherTemp: String         = "",
        weatherCondition: String    = "",
        weatherPrecipWind: String   = "",
        sunTimes: String            = "",
        nearestHospital: String     = "",
        castCallEntries: [CastCallEntry] = [],
        crewCallEntries: [CrewCallEntry] = [],
        productionNotes: [String]   = [],
        locations:       [Location] = [],
        castOverride:    [String]?  = nil,
        crewOverride:    [String]?  = nil,
        crewIDOverride:  [UUID]?    = nil,
        crewOneOffs:     [String]?  = nil,
        notes:           String     = ""
    ) {
        self.generalCallTime    = generalCallTime
        self.workDaySchedule    = workDaySchedule
        self.readyToShootTime   = readyToShootTime
        self.lunchTime          = lunchTime
        self.snackTime          = snackTime
        self.dinnerTime         = dinnerTime
        self.quoteOfTheDay      = quoteOfTheDay
        self.prodManagerContact = prodManagerContact
        self.adContact          = adContact
        self.weatherTemp        = weatherTemp
        self.weatherCondition   = weatherCondition
        self.weatherPrecipWind  = weatherPrecipWind
        self.sunTimes           = sunTimes
        self.nearestHospital    = nearestHospital
        self.castCallEntries    = castCallEntries
        self.crewCallEntries    = crewCallEntries
        self.productionNotes    = productionNotes
        self.locations          = locations
        self.castOverride       = castOverride
        self.crewOverride       = crewOverride
        self.crewIDOverride     = crewIDOverride
        self.crewOneOffs        = crewOneOffs
        self.notes              = notes
    }

    enum CodingKeys: String, CodingKey {
        case generalCallTime, workDaySchedule, readyToShootTime, lunchTime, snackTime, dinnerTime
        case quoteOfTheDay, prodManagerContact, adContact, weatherTemp, weatherCondition, weatherPrecipWind, sunTimes, nearestHospital
        case castCallEntries, crewCallEntries, productionNotes, locations, castOverride, crewOverride, crewIDOverride, crewOneOffs, notes
    }

    init(from decoder: Decoder) throws {
        let c              = try decoder.container(keyedBy: CodingKeys.self)
        generalCallTime    = try c.decodeIfPresent(String.self, forKey: .generalCallTime) ?? ""
        workDaySchedule    = try c.decodeIfPresent(String.self, forKey: .workDaySchedule) ?? ""
        readyToShootTime   = try c.decodeIfPresent(String.self, forKey: .readyToShootTime) ?? ""
        lunchTime          = try c.decodeIfPresent(String.self, forKey: .lunchTime) ?? ""
        snackTime          = try c.decodeIfPresent(String.self, forKey: .snackTime) ?? ""
        dinnerTime         = try c.decodeIfPresent(String.self, forKey: .dinnerTime) ?? ""
        quoteOfTheDay      = try c.decodeIfPresent(String.self, forKey: .quoteOfTheDay) ?? ""
        prodManagerContact = try c.decodeIfPresent(String.self, forKey: .prodManagerContact) ?? ""
        adContact          = try c.decodeIfPresent(String.self, forKey: .adContact) ?? ""
        weatherTemp        = try c.decodeIfPresent(String.self, forKey: .weatherTemp) ?? ""
        weatherCondition   = try c.decodeIfPresent(String.self, forKey: .weatherCondition) ?? ""
        weatherPrecipWind  = try c.decodeIfPresent(String.self, forKey: .weatherPrecipWind) ?? ""
        sunTimes           = try c.decodeIfPresent(String.self, forKey: .sunTimes) ?? ""
        nearestHospital    = try c.decodeIfPresent(String.self, forKey: .nearestHospital) ?? ""
        castCallEntries    = try c.decodeIfPresent([CastCallEntry].self, forKey: .castCallEntries) ?? []
        crewCallEntries    = try c.decodeIfPresent([CrewCallEntry].self, forKey: .crewCallEntries) ?? []
        productionNotes    = try c.decodeIfPresent([String].self, forKey: .productionNotes) ?? []
        locations          = try c.decodeIfPresent([Location].self, forKey: .locations) ?? []
        castOverride       = try c.decodeIfPresent([String].self, forKey: .castOverride)
        crewOverride       = try c.decodeIfPresent([String].self, forKey: .crewOverride)
        crewIDOverride     = try c.decodeIfPresent([UUID].self, forKey: .crewIDOverride)
        crewOneOffs        = try c.decodeIfPresent([String].self, forKey: .crewOneOffs)
        notes              = try c.decodeIfPresent(String.self, forKey: .notes) ?? ""
    }

    /// Resolves the raw character names (auto-pulled from scenes, or the manually-edited
    /// override) to "Actor — Character" using the *current* cast list.
    func resolvedCast(from scenes: [Scene], productionInfo: ProductionInfo? = nil) -> [String] {
        if !castCallEntries.isEmpty {
            return castCallEntries.map { entry in
                entry.actorName.isEmpty ? entry.characterName : "\(entry.actorName) — \(entry.characterName)"
            }
        }
        let characters = castOverride ?? Array(Set(scenes.flatMap { $0.cast })).sorted()
        guard let production = productionInfo, !production.castList.isEmpty else {
            return characters
        }
        return characters.map { character in
            if let match = production.castList.first(where: {
                $0.characterName.trimmingCharacters(in: .whitespaces)
                    .caseInsensitiveCompare(character.trimmingCharacters(in: .whitespaces)) == .orderedSame
            }) {
                return match.displayString
            }
            return character
        }
    }

    /// Resolves selected crew to "Name — Role" using the *current* roster for anyone selected
    /// by ID.
    func resolvedCrew(productionInfo: ProductionInfo) -> [String] {
        if !crewCallEntries.isEmpty {
            return crewCallEntries.map { "\($0.name) — \($0.role)" }
        }
        if crewIDOverride != nil || crewOneOffs != nil {
            let roster = productionInfo.crew
            let selected = (crewIDOverride ?? []).compactMap { id in
                roster.first(where: { $0.id == id })?.displayString
            }
            return selected + (crewOneOffs ?? [])
        }
        if let legacy = crewOverride { return legacy }
        return productionInfo.crew
            .filter { $0.isDailyDefault }
            .map    { $0.displayString }
    }
}

// MARK: - CrewMember

struct CrewMember: Identifiable, Codable, Hashable {
    let id: UUID
    var name:           String
    var role:           String
    var phone:          String
    var isDailyDefault: Bool

    init(name: String = "", role: String = "", phone: String = "", isDailyDefault: Bool = false) {
        self.id             = UUID()
        self.name           = name
        self.role           = role
        self.phone          = phone
        self.isDailyDefault = isDailyDefault
    }

    enum CodingKeys: String, CodingKey {
        case id, name, role, phone, isDailyDefault
    }

    init(from decoder: Decoder) throws {
        let c          = try decoder.container(keyedBy: CodingKeys.self)
        id             = try c.decode(UUID.self,   forKey: .id)
        name           = try c.decode(String.self, forKey: .name)
        role           = try c.decode(String.self, forKey: .role)
        phone          = try c.decodeIfPresent(String.self, forKey: .phone) ?? ""
        isDailyDefault = try c.decodeIfPresent(Bool.self, forKey: .isDailyDefault) ?? false
    }

    var displayString: String {
        role.isEmpty ? name : "\(name) — \(role)"
    }
}

// MARK: - DateRange (actor unavailability)

struct DateRange: Identifiable, Codable, Hashable {
    let id: UUID
    var start: Date
    var end: Date

    init(start: Date, end: Date) {
        self.id    = UUID()
        self.start = start
        self.end   = max(start, end)
    }

    func contains(_ date: Date) -> Bool {
        let cal = Calendar.current
        let d = cal.startOfDay(for: date)
        return d >= cal.startOfDay(for: start) && d <= cal.startOfDay(for: end)
    }
}

// MARK: - CastMember

struct CastMember: Identifiable, Codable, Hashable {
    let id: UUID
    var actorName:     String
    var characterName: String
    var unavailableRanges: [DateRange]

    init(actorName: String = "", characterName: String = "", unavailableRanges: [DateRange] = []) {
        self.id                = UUID()
        self.actorName         = actorName
        self.characterName     = characterName
        self.unavailableRanges = unavailableRanges
    }

    enum CodingKeys: String, CodingKey {
        case id, actorName, characterName, unavailableRanges
    }

    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id                = try c.decode(UUID.self,   forKey: .id)
        actorName         = try c.decode(String.self, forKey: .actorName)
        characterName     = try c.decode(String.self, forKey: .characterName)
        unavailableRanges = try c.decodeIfPresent([DateRange].self, forKey: .unavailableRanges) ?? []
    }

    var displayString: String {
        actorName.isEmpty ? characterName : "\(actorName) — \(characterName)"
    }
}

// MARK: - Scene script order

extension Scene {
    /// A numeric-aware sort key. If `sceneNumber` is populated, uses `parseSceneNumber`
    /// so scenes sort in true script order — 12, 12A, 12B, 13.
    /// Falls back to parsing a leading scene number in the title for legacy scenes.
    var scriptOrderKey: (Int, String) {
        if let parsed = Scene.parseSceneNumber(sceneNumber) {
            return (parsed.number, parsed.letter)
        }
        let trimmed = title.trimmingCharacters(in: .whitespaces)
        let pattern = #"^(\d+)([A-Za-z]*)\."#
        if let regex = try? NSRegularExpression(pattern: pattern),
           let match = regex.firstMatch(in: trimmed, range: NSRange(trimmed.startIndex..., in: trimmed)),
           let numRange = Range(match.range(at: 1), in: trimmed) {
            let num = Int(trimmed[numRange]) ?? Int.max
            let letterRange = Range(match.range(at: 2), in: trimmed)
            let letter = letterRange.map { String(trimmed[$0]) } ?? ""
            return (num, letter)
        }
        return (Int.max, trimmed)
    }
}

// MARK: - Scene tooltip

extension Scene {
    var tooltipText: String {
        var lines: [String] = [displayTitle]
        if !cast.isEmpty {
            lines.append("Cast: " + cast.joined(separator: ", "))
        }
        let trimmedSummary = summary.trimmingCharacters(in: .whitespacesAndNewlines)
        if !trimmedSummary.isEmpty {
            lines.append(trimmedSummary)
        }
        return lines.joined(separator: "\n")
    }
}

// MARK: - ProductionInfo

struct ProductionInfo: Codable, Equatable {
    var companyName:   String
    var directorName:  String
    var directorPhone: String
    var producerName:  String
    var producerPhone: String
    var adName:        String
    var adPhone:       String
    var contactNumber: String
    var defaultLunchTime: String
    var crew:          [CrewMember]
    var castList:      [CastMember]
    var locationRoster: [Location]
    var scheduleLock: ScheduleLock?

    init(
        companyName:   String = "",
        directorName:  String = "",
        directorPhone: String = "",
        producerName:  String = "",
        producerPhone: String = "",
        adName:        String = "",
        adPhone:       String = "",
        contactNumber: String = "",
        defaultLunchTime: String = "01:30 PM",
        crew:          [CrewMember] = [],
        castList:      [CastMember] = [],
        locationRoster: [Location] = [],
        scheduleLock:  ScheduleLock? = nil
    ) {
        self.companyName      = companyName
        self.directorName     = directorName
        self.directorPhone    = directorPhone
        self.producerName     = producerName
        self.producerPhone    = producerPhone
        self.adName           = adName
        self.adPhone          = adPhone
        self.contactNumber    = contactNumber
        self.defaultLunchTime = defaultLunchTime
        self.crew             = crew
        self.castList         = castList
        self.locationRoster   = locationRoster
        self.scheduleLock     = scheduleLock
    }

    enum CodingKeys: String, CodingKey {
        case companyName, directorName, directorPhone, producerName, producerPhone, adName, adPhone, contactNumber, defaultLunchTime, crew, castList, locationRoster, scheduleLock
    }

    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        companyName      = try c.decode(String.self, forKey: .companyName)
        directorName     = try c.decode(String.self, forKey: .directorName)
        directorPhone    = try c.decodeIfPresent(String.self, forKey: .directorPhone) ?? ""
        producerName     = try c.decodeIfPresent(String.self, forKey: .producerName) ?? ""
        producerPhone    = try c.decodeIfPresent(String.self, forKey: .producerPhone) ?? ""
        adName           = try c.decodeIfPresent(String.self, forKey: .adName) ?? ""
        adPhone          = try c.decodeIfPresent(String.self, forKey: .adPhone) ?? ""
        contactNumber    = try c.decodeIfPresent(String.self, forKey: .contactNumber) ?? ""
        defaultLunchTime = try c.decodeIfPresent(String.self, forKey: .defaultLunchTime) ?? "01:30 PM"
        crew             = try c.decode([CrewMember].self, forKey: .crew)
        castList         = try c.decode([CastMember].self, forKey: .castList)
        locationRoster   = try c.decodeIfPresent([Location].self, forKey: .locationRoster) ?? []
        scheduleLock     = try c.decodeIfPresent(ScheduleLock.self, forKey: .scheduleLock)
    }
}

// MARK: - ScheduleLock

struct ScheduleLock: Codable, Equatable {
    var lockedAt: Date
    var workingDays: [String: [Date]]
}

// MARK: - ShootDay

struct ShootDay: Identifiable, Codable {
    let id:        UUID
    var date:      Date
    var scenes:    [Scene]       = []
    var callSheet: CallSheetData = CallSheetData()
    var isBlackout: Bool = false

    init(date: Date, scenes: [Scene] = [], callSheet: CallSheetData = CallSheetData(), isBlackout: Bool = false) {
        self.id         = UUID()
        self.date       = date
        self.scenes     = scenes
        self.callSheet  = callSheet
        self.isBlackout = isBlackout
    }

    enum CodingKeys: String, CodingKey {
        case id, date, scenes, callSheet, isBlackout
    }

    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id         = try c.decode(UUID.self, forKey: .id)
        date       = try c.decode(Date.self, forKey: .date)
        scenes     = try c.decode([Scene].self, forKey: .scenes)
        callSheet  = try c.decode(CallSheetData.self, forKey: .callSheet)
        isBlackout = try c.decodeIfPresent(Bool.self, forKey: .isBlackout) ?? false
    }

    var totalDuration:      Int { scenes.reduce(0) { $0 + $1.duration } }
    var totalEstimatedTime: Int { scenes.reduce(0) { $0 + $1.estimatedTime } }

    var dayScenes:       [Scene] { scenes.filter { $0.dayNightType == .day } }
    var nightScenes:     [Scene] { scenes.filter { $0.dayNightType == .night } }
    var dawnScenes:      [Scene] { scenes.filter { $0.dayNightType == .dawn } }
    var duskScenes:      [Scene] { scenes.filter { $0.dayNightType == .dusk } }
    var afternoonScenes: [Scene] { scenes.filter { $0.dayNightType == .afternoon } }
    var customScenes:    [Scene] { scenes.filter { $0.dayNightType == .custom } }

    var totalDayDuration:       Int { dayScenes.reduce(0)       { $0 + $1.duration } }
    var totalNightDuration:     Int { nightScenes.reduce(0)     { $0 + $1.duration } }
    var totalDawnDuration:      Int { dawnScenes.reduce(0)      { $0 + $1.duration } }
    var totalDuskDuration:      Int { duskScenes.reduce(0)      { $0 + $1.duration } }
    var totalAfternoonDuration: Int { afternoonScenes.reduce(0) { $0 + $1.duration } }

    var allCast: [String] {
        Array(Set(scenes.flatMap { $0.cast })).sorted()
    }

    var hasCallSheetData: Bool {
        !callSheet.generalCallTime.isEmpty ||
        !callSheet.lunchTime.isEmpty       ||
        !callSheet.locations.isEmpty       ||
        !callSheet.castCallEntries.isEmpty ||
        !callSheet.notes.isEmpty
    }
}

// MARK: - ProjectData

struct ProjectData: Codable {
    var allScenes:          [Scene]
    var shootDays:          [ShootDay]
    var projectTitle:       String
    var createdDate:        Date
    var isShiftModeEnabled: Bool?
    var productionInfo:     ProductionInfo?

    init(
        allScenes:          [Scene],
        shootDays:          [ShootDay],
        projectTitle:       String = "Untitled Movie",
        isShiftModeEnabled: Bool?  = false,
        createdDate:        Date   = Date(),
        productionInfo:     ProductionInfo? = nil
    ) {
        self.allScenes          = allScenes
        self.shootDays          = shootDays
        self.projectTitle       = projectTitle
        self.createdDate        = createdDate
        self.isShiftModeEnabled = isShiftModeEnabled
        self.productionInfo     = productionInfo
    }
}

// MARK: - Legacy Support

struct LegacyProjectData: Codable {
    var allScenes: [Scene]
    var shootDays: [ShootDay]
}
