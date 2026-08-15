// FinalDraftParser.swift
// Parses Final Draft .fdx files and extracts scene information

import Foundation

struct FinalDraftParser {
    
    enum TimeOfDay {
        case day
        case night
        case dawn
        case dusk
        case afternoon
        case unknown
        
        init(from text: String) {
            let lowercased = text.lowercased()
                .folding(options: .diacriticInsensitive, locale: .current)
            if lowercased.contains("dawn") || lowercased.contains("sunrise") || lowercased.contains("amanecer") || lowercased.contains("madrugada") || lowercased.contains("alba") {
                self = .dawn
            } else if lowercased.contains("dusk") || lowercased.contains("sunset") || lowercased.contains("twilight") || lowercased.contains("anochecer") || lowercased.contains("crepusculo") || lowercased.contains("ocaso") {
                self = .dusk
            } else if lowercased.contains("afternoon") || lowercased.contains("tarde") || lowercased.contains("atardecer") {
                self = .afternoon
            } else if lowercased.contains("night") || lowercased.contains("evening") || lowercased.contains("noche") || lowercased.contains("medianoche") {
                self = .night
            } else if lowercased.contains("day") || lowercased.contains("morning") || lowercased.contains("dia") || lowercased.contains("manana") {
                self = .day
            } else {
                self = .unknown
            }
        }
    }
    
    struct ParsedScene {
        let sceneNumber: String
        let location: String
        let timeOfDay: TimeOfDay
        let fullHeading: String
    }
    
    /// Parse an FDX file and extract all scenes
    static func parseScenes(from url: URL) throws -> [ParsedScene] {
        let data = try Data(contentsOf: url)
        let parser = XMLParser(data: data)
        let delegate = FDXParserDelegate()
        parser.delegate = delegate
        
        guard parser.parse() else {
            if let error = parser.parserError {
                throw error
            }
            throw NSError(domain: "FinalDraftParser", code: 1, userInfo: [NSLocalizedDescriptionKey: "Failed to parse FDX file"])
        }
        
        return delegate.scenes
    }
    
    /// Extract scene components from a scene heading
    /// Examples:
    /// "3. EXT. WOODS - DAY" -> (number: "3", location: "EXT. WOODS", time: .day)
    /// "24. INT. HOTEL RENAISANCE, HABITACIÓN, BAÑO. NOCHE." -> (number: "24", location: "INT. HOTEL RENAISANCE, HABITACIÓN, BAÑO", time: .night)
    /// "3. EXT. HOTEL RENAISANCE, AZOTEA. TARDE." -> (number: "3", location: "EXT. HOTEL RENAISANCE, AZOTEA", time: .afternoon)
    static func parseSceneHeading(_ heading: String) -> (number: String?, location: String, timeOfDay: TimeOfDay) {
        var workingHeading = heading.trimmingCharacters(in: .whitespacesAndNewlines)
        
        // Extract scene number if present (e.g., "3.", "12A.", "5B.")
        var sceneNumber: String? = nil
        let numberPattern = #"^(\d+[A-Za-z]?)\.\s*"#
        if let regex = try? NSRegularExpression(pattern: numberPattern),
           let match = regex.firstMatch(in: workingHeading, range: NSRange(workingHeading.startIndex..., in: workingHeading)) {
            if let range = Range(match.range(at: 1), in: workingHeading) {
                sceneNumber = String(workingHeading[range])
                // Remove the number from the heading
                if let fullRange = Range(match.range, in: workingHeading) {
                    workingHeading.removeSubrange(fullRange)
                }
            }
        }
        
        var location = workingHeading
        var timeOfDay = TimeOfDay.unknown
        
        // Check for trailing time of day (separated by -, –, —, ., /, or space) in English or Spanish
        let timePattern = #"[-–—./,\s]+\s*\b(day|night|morning|afternoon|evening|dusk|dawn|dia|día|noche|tarde|amanecer|anochecer|madrugada|alba|atardecer|crepusculo|crepúsculo|ocaso|medianoche)\b\.?\s*$"#
        if let regex = try? NSRegularExpression(pattern: timePattern, options: .caseInsensitive),
           let match = regex.firstMatch(in: workingHeading, range: NSRange(workingHeading.startIndex..., in: workingHeading)),
           let fullMatchRange = Range(match.range, in: workingHeading),
           let timeWordRange = Range(match.range(at: 1), in: workingHeading) {
            let timeStr = String(workingHeading[timeWordRange])
            timeOfDay = TimeOfDay(from: timeStr)
            workingHeading.removeSubrange(fullMatchRange)
            location = workingHeading
        } else {
            // Split by hyphen or dash if present
            let components = workingHeading.components(separatedBy: CharacterSet(charactersIn: "-–—"))
            if components.count >= 2 {
                location = components.dropLast().joined(separator: "-").trimmingCharacters(in: .whitespacesAndNewlines)
                let timeString = components.last?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
                timeOfDay = TimeOfDay(from: timeString)
            } else {
                timeOfDay = TimeOfDay(from: workingHeading)
            }
        }
        
        // Clean up location (remove extra spaces, trailing punctuation like trailing dots or hyphens)
        location = location.trimmingCharacters(in: CharacterSet(charactersIn: " -–—.,/"))
        location = location.replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
            .trimmingCharacters(in: .whitespacesAndNewlines)
        
        return (sceneNumber, location, timeOfDay)
    }
}

// MARK: - XML Parser Delegate

private class FDXParserDelegate: NSObject, XMLParserDelegate {
    var scenes: [FinalDraftParser.ParsedScene] = []
    
    private var currentElement = ""
    private var currentType = ""
    private var currentText = ""
    private var inSceneHeading = false
    private var inText = false
    private var textElements: [String] = []
    private var depth = 0  // Track nesting depth to ignore nested Paragraphs
    
    func parser(_ parser: XMLParser, didStartElement elementName: String, namespaceURI: String?, qualifiedName qName: String?, attributes attributeDict: [String : String] = [:]) {
        currentElement = elementName
        
        if elementName == "Paragraph" {
            if !inSceneHeading {
                // This is a top-level Paragraph
                currentType = attributeDict["Type"] ?? ""
                
                if currentType == "Scene Heading" {
                    inSceneHeading = true
                    textElements = []
                }
            } else {
                // This is a nested Paragraph (inside SceneArcBeats) - ignore it
                depth += 1
            }
        } else if elementName == "Text" && inSceneHeading && depth == 0 {
            // Only collect Text if we're in a Scene Heading and NOT in a nested paragraph
            inText = true
            currentText = ""
        }
    }
    
    func parser(_ parser: XMLParser, foundCharacters string: String) {
        if inText {
            currentText += string
        }
    }
    
    func parser(_ parser: XMLParser, didEndElement elementName: String, namespaceURI: String?, qualifiedName qName: String?) {
        if elementName == "Text" && inText {
            if !currentText.isEmpty {
                textElements.append(currentText)
            }
            currentText = ""
            inText = false
        } else if elementName == "Paragraph" {
            if depth > 0 {
                // Closing a nested paragraph
                depth -= 1
            } else if inSceneHeading {
                // Closing the Scene Heading paragraph
                let rawHeading = textElements.joined().trimmingCharacters(in: .whitespacesAndNewlines)
                let heading = rawHeading.uppercased()  // AUTO-CAPITALIZE
                
                if !heading.isEmpty {
                    let (number, location, timeOfDay) = FinalDraftParser.parseSceneHeading(heading)
                    let finalNumber = number ?? "\(scenes.count + 1)"
                    
                    let scene = FinalDraftParser.ParsedScene(
                        sceneNumber: finalNumber,
                        location: location,
                        timeOfDay: timeOfDay,
                        fullHeading: heading
                    )
                    
                    scenes.append(scene)
                }
                
                inSceneHeading = false
                currentType = ""
                textElements = []
            }
        }
        
        currentElement = ""
    }
    
    func parser(_ parser: XMLParser, parseErrorOccurred parseError: Error) {
        print("XML Parse Error: \(parseError.localizedDescription)")
    }
}
