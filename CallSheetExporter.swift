// CallSheetExporter.swift
// Generates a 100% English Call Sheet PDF matching standard film industry layout.

import AppKit
import SwiftUI
import UniformTypeIdentifiers

// MARK: - CallSheetExporter

class CallSheetExporter {

    private static let pageWidth:    CGFloat = 612   // US Letter portrait
    private static let pageHeight:   CGFloat = 792
    private static let margin:       CGFloat = 28
    private static let contentWidth: CGFloat = pageWidth - 2 * margin

    // Fonts
    private static let fontBannerTitle = NSFont.boldSystemFont(ofSize: 13)
    private static let fontCallBig     = NSFont.boldSystemFont(ofSize: 26)
    private static let fontCallSub     = NSFont.systemFont(ofSize: 9)
    private static let fontQuote       = NSFontManager.shared.font(withFamily: "Helvetica", traits: .italicFontMask, weight: 5, size: 8.5) ?? NSFont.systemFont(ofSize: 8.5)
    private static let fontSectionHdr  = NSFont.boldSystemFont(ofSize: 9.5)
    private static let fontTableHdr    = NSFont.boldSystemFont(ofSize: 8.5)
    private static let fontBoldBody    = NSFont.boldSystemFont(ofSize: 8.5)
    private static let fontRegularBody = NSFont.systemFont(ofSize: 8)
    private static let fontSmall       = NSFont.systemFont(ofSize: 7.5)
    private static let fontTiny        = NSFont.systemFont(ofSize: 7)

    // Colors (Clean modern monochrome & subtle grays, NO orange)
    private static let colorHeaderBar   = NSColor(white: 0.88, alpha: 1)
    private static let colorGrayHeader  = NSColor(white: 0.93, alpha: 1)
    private static let colorBorder      = NSColor(white: 0.25, alpha: 1)
    private static let colorBlack       = NSColor.black
    private static let colorDark        = NSColor(white: 0.15, alpha: 1)

    // MARK: - Entry point

    static func generatePDF(
        shootDay: ShootDay,
        productionInfo: ProductionInfo,
        projectTitle: String,
        dayNumber: Int? = nil,
        totalProductionDays: Int = 0,
        language: AppLanguage = .english
    ) -> Data? {
        let pdfData = NSMutableData()
        guard let consumer = CGDataConsumer(data: pdfData) else { return nil }
        var mediaBox = CGRect(x: 0, y: 0, width: pageWidth, height: pageHeight)
        guard let ctx = CGContext(consumer: consumer, mediaBox: &mediaBox, nil) else { return nil }

        var y: CGFloat = pageHeight - margin
        var pageNumber = 0

        func beginPage() {
            pageNumber += 1
            ctx.beginPDFPage(nil)
            NSGraphicsContext.saveGraphicsState()
            NSGraphicsContext.current = NSGraphicsContext(cgContext: ctx, flipped: false)
            y = pageHeight - margin
        }

        func endPage() {
            NSGraphicsContext.restoreGraphicsState()
            ctx.endPDFPage()
        }

        func ensureRoom(_ height: CGFloat) {
            if y - height < margin + 10 {
                endPage()
                beginPage()
            }
        }

        beginPage()

        // 1. Top Header Banner
        y = drawTopBanner(y: y, shootDay: shootDay, dayNumber: dayNumber)

        // 2. General Call Banner (with Quote of the day if exists)
        y = drawGeneralCallBanner(y: y, callSheet: shootDay.callSheet)

        // 3. Three-Block Info Row (Shooting Contacts, Milestones, Weather)
        y = drawThreeBlockInfoRow(y: y, shootDay: shootDay, productionInfo: productionInfo)

        // 4. Nearest Hospital
        y = drawHospitalBar(y: y, callSheet: shootDay.callSheet)

        // 5. Scenes Breakdown Table
        y = drawScenesTable(y: y, shootDay: shootDay, ensureRoom: ensureRoom)

        // 6. Cast Call Table
        y = drawCastTable(y: y, shootDay: shootDay, productionInfo: productionInfo, ensureRoom: ensureRoom)

        // 7. Crew Call Times (Role: Name + Call Time)
        y = drawCrewTable(y: y, shootDay: shootDay, productionInfo: productionInfo, ensureRoom: ensureRoom)

        // 8. General Notes (Unified in one single block)
        y = drawProductionNotes(y: y, callSheet: shootDay.callSheet, ensureRoom: ensureRoom)

        endPage()
        ctx.closePDF()
        return pdfData as Data
    }

    // MARK: - 1. Top Banner

    private static func drawTopBanner(y: CGFloat, shootDay: ShootDay, dayNumber: Int?) -> CGFloat {
        let height: CGFloat = 22
        let rect = CGRect(x: margin, y: y - height, width: contentWidth, height: height)

        colorHeaderBar.setFill()
        NSBezierPath(rect: rect).fill()
        colorBorder.setStroke()
        let border = NSBezierPath(rect: rect); border.lineWidth = 1; border.stroke()

        let numStr = dayNumber.map { String(format: "%02d", $0) } ?? "01"
        let fullText = "CALL SHEET #\(numStr) — \(formattedFullDate(shootDay.date))"

        let para = NSMutableParagraphStyle(); para.alignment = .center
        let attr: [NSAttributedString.Key: Any] = [
            .font: fontBannerTitle,
            .foregroundColor: colorBlack,
            .paragraphStyle: para
        ]
        NSAttributedString(string: fullText, attributes: attr)
            .draw(in: CGRect(x: rect.minX, y: rect.midY - 7.5, width: rect.width, height: 16))

        return y - height
    }

    // MARK: - 2. General Call Banner

    private static func drawGeneralCallBanner(y: CGFloat, callSheet: CallSheetData) -> CGFloat {
        let hasQuote = !callSheet.quoteOfTheDay.trimmingCharacters(in: .whitespaces).isEmpty
        let height: CGFloat = hasQuote ? 72 : 62
        let rect = CGRect(x: margin, y: y - height, width: contentWidth, height: height)

        colorGrayHeader.setFill()
        NSBezierPath(rect: rect).fill()
        colorBorder.setStroke()
        let border = NSBezierPath(rect: rect); border.lineWidth = 1; border.stroke()

        let para = NSMutableParagraphStyle(); para.alignment = .center

        // Subtitle "GENERAL CALL"
        let subAttr: [NSAttributedString.Key: Any] = [
            .font: fontSectionHdr,
            .foregroundColor: colorDark,
            .paragraphStyle: para
        ]
        NSAttributedString(string: "GENERAL CALL", attributes: subAttr)
            .draw(in: CGRect(x: rect.minX, y: rect.maxY - 14, width: rect.width, height: 12))

        // Big Call Time (12h)
        let callTime = callSheet.generalCallTime.isEmpty ? "07:30 AM" : callSheet.generalCallTime
        let bigAttr: [NSAttributedString.Key: Any] = [
            .font: fontCallBig,
            .foregroundColor: colorBlack,
            .paragraphStyle: para
        ]
        NSAttributedString(string: callTime, attributes: bigAttr)
            .draw(in: CGRect(x: rect.minX, y: rect.midY - (hasQuote ? 18 : 14), width: rect.width, height: 28))

        // Schedule range & Quote
        var bottomY = rect.minY + 4
        if hasQuote {
            let quoteAttr: [NSAttributedString.Key: Any] = [
                .font: fontQuote,
                .foregroundColor: colorBlack,
                .paragraphStyle: para
            ]
            let quoteStr = "“Quote of the day: \(callSheet.quoteOfTheDay)”"
            NSAttributedString(string: quoteStr, attributes: quoteAttr)
                .draw(in: CGRect(x: rect.minX + 8, y: bottomY, width: rect.width - 16, height: 11))
            bottomY += 12
        }

        if !callSheet.workDaySchedule.isEmpty {
            let italicFont = NSFontManager.shared.font(withFamily: fontCallSub.familyName ?? "Helvetica", traits: .italicFontMask, weight: 5, size: 8.5) ?? fontCallSub
            let schedAttr: [NSAttributedString.Key: Any] = [
                .font: italicFont,
                .foregroundColor: colorDark,
                .paragraphStyle: para
            ]
            let rawSched = callSheet.workDaySchedule
                .replacingOccurrences(of: "Jornada de ", with: "")
                .replacingOccurrences(of: "Jornada ", with: "")
                .replacingOccurrences(of: "Schedule: ", with: "")
            let schedText = "Schedule: \(rawSched)"
            NSAttributedString(string: schedText, attributes: schedAttr)
                .draw(in: CGRect(x: rect.minX, y: bottomY, width: rect.width, height: 11))
        }

        return y - height
    }

    // MARK: - 3. Three-Block Info Row

    private static func drawThreeBlockInfoRow(y: CGFloat, shootDay: ShootDay, productionInfo: ProductionInfo) -> CGFloat {
        let height: CGFloat = 68
        let w1: CGFloat = contentWidth * 0.33
        let w2: CGFloat = contentWidth * 0.33
        let w3: CGFloat = contentWidth - w1 - w2

        let r1 = CGRect(x: margin,           y: y - height, width: w1, height: height)
        let r2 = CGRect(x: margin + w1,      y: y - height, width: w2, height: height)
        let r3 = CGRect(x: margin + w1 + w2, y: y - height, width: w3, height: height)

        for r in [r1, r2, r3] {
            let b = NSBezierPath(rect: r); b.lineWidth = 0.75; colorBorder.setStroke(); b.stroke()
        }

        let cs = shootDay.callSheet

        // --- Box 1: SHOOTING CONTACTS ---
        let cPara = NSMutableParagraphStyle(); cPara.alignment = .center
        let cTitleAttr: [NSAttributedString.Key: Any] = [.font: fontSectionHdr, .foregroundColor: colorBlack, .paragraphStyle: cPara]
        NSAttributedString(string: "☎ SHOOTING CONTACTS", attributes: cTitleAttr)
            .draw(in: CGRect(x: r1.minX + 4, y: r1.maxY - 13, width: r1.width - 8, height: 12))

        let bPara = NSMutableParagraphStyle(); bPara.alignment = .center
        let nameAttr: [NSAttributedString.Key: Any] = [.font: fontBoldBody, .foregroundColor: colorBlack, .paragraphStyle: bPara]
        let subAttr:  [NSAttributedString.Key: Any] = [.font: fontRegularBody, .foregroundColor: colorDark, .paragraphStyle: bPara]

        var cY = r1.maxY - 25

        // 1. Producer
        var prodContact = ""
        if !productionInfo.producerName.isEmpty {
            let phone = productionInfo.producerPhone.isEmpty ? "" : " - Tel: \(productionInfo.producerPhone)"
            prodContact = "\(productionInfo.producerName)\(phone)"
        } else if !cs.prodManagerContact.isEmpty {
            prodContact = cs.prodManagerContact
        }

        if !prodContact.isEmpty {
            NSAttributedString(string: "PRODUCER", attributes: nameAttr)
                .draw(in: CGRect(x: r1.minX + 4, y: cY, width: r1.width - 8, height: 10))
            cY -= 10
            NSAttributedString(string: prodContact, attributes: subAttr)
                .draw(in: CGRect(x: r1.minX + 4, y: cY, width: r1.width - 8, height: 10))
            cY -= 10
        }

        // 2. 1st AD or Director
        var secondRole = ""
        var secondContact = ""
        if !productionInfo.adName.isEmpty {
            secondRole = "1ST AD"
            let phone = productionInfo.adPhone.isEmpty ? "" : " - Tel: \(productionInfo.adPhone)"
            secondContact = "\(productionInfo.adName)\(phone)"
        } else if !productionInfo.directorName.isEmpty {
            secondRole = "DIRECTOR"
            let phone = productionInfo.directorPhone.isEmpty ? "" : " - Tel: \(productionInfo.directorPhone)"
            secondContact = "\(productionInfo.directorName)\(phone)"
        } else if !cs.adContact.isEmpty {
            secondRole = "1ST AD"
            secondContact = cs.adContact
        }

        if !secondContact.isEmpty {
            NSAttributedString(string: secondRole, attributes: nameAttr)
                .draw(in: CGRect(x: r1.minX + 4, y: cY, width: r1.width - 8, height: 10))
            cY -= 10
            NSAttributedString(string: secondContact, attributes: subAttr)
                .draw(in: CGRect(x: r1.minX + 4, y: cY, width: r1.width - 8, height: 10))
        }

        // --- Box 2: MILESTONES & MEAL TIMES ---
        var milestones: [(String, String)] = []
        if !cs.readyToShootTime.isEmpty { milestones.append(("READY TO SHOOT", cs.readyToShootTime)) }
        if !cs.lunchTime.isEmpty        { milestones.append(("LUNCH", cs.lunchTime)) }
        if !cs.snackTime.isEmpty        { milestones.append(("SNACK", cs.snackTime)) }
        if !cs.dinnerTime.isEmpty       { milestones.append(("WRAP", cs.dinnerTime)) }

        if milestones.isEmpty {
            milestones = [("READY TO SHOOT", ""), ("LUNCH", ""), ("SNACK", ""), ("WRAP", "")]
        }

        let rowH = height / CGFloat(milestones.count)
        for (i, m) in milestones.enumerated() {
            let mRect = CGRect(x: r2.minX, y: r2.maxY - CGFloat(i + 1) * rowH, width: r2.width, height: rowH)
            let b = NSBezierPath(rect: mRect); b.lineWidth = 0.5; colorBorder.setStroke(); b.stroke()

            let mLabel = "\(m.0)................................."
            let lAttr: [NSAttributedString.Key: Any] = [.font: fontBoldBody, .foregroundColor: colorBlack]
            NSAttributedString(string: mLabel, attributes: lAttr)
                .draw(in: CGRect(x: mRect.minX + 6, y: mRect.midY - 5, width: mRect.width - 52, height: 11))

            let rPara = NSMutableParagraphStyle(); rPara.alignment = .center
            let vAttr: [NSAttributedString.Key: Any] = [.font: fontBoldBody, .foregroundColor: colorBlack, .paragraphStyle: rPara]
            NSAttributedString(string: m.1, attributes: vAttr)
                .draw(in: CGRect(x: mRect.maxX - 50, y: mRect.midY - 5, width: 46, height: 11))
        }

        // --- Box 3: WEATHER FORECAST ---
        NSAttributedString(string: "☁ WEATHER FORECAST", attributes: cTitleAttr)
            .draw(in: CGRect(x: r3.minX + 4, y: r3.maxY - 13, width: r3.width - 8, height: 12))

        var wY = r3.maxY - 25
        var weatherLines: [String] = []
        if !cs.weatherTemp.isEmpty       { weatherLines.append("Temp: \(cs.weatherTemp)") }
        if !cs.weatherCondition.isEmpty  { weatherLines.append(cs.weatherCondition) }
        if !cs.weatherPrecipWind.isEmpty { weatherLines.append(cs.weatherPrecipWind) }
        if !cs.sunTimes.isEmpty          { weatherLines.append(cs.sunTimes) }

        for line in weatherLines {
            let bold = line.uppercased().contains("SUNRISE") || line.uppercased().contains("AMANECE")
            let attr: [NSAttributedString.Key: Any] = [
                .font: bold ? fontBoldBody : fontRegularBody,
                .foregroundColor: colorBlack,
                .paragraphStyle: cPara
            ]
            NSAttributedString(string: line, attributes: attr)
                .draw(in: CGRect(x: r3.minX + 4, y: wY, width: r3.width - 8, height: 10))
            wY -= 10
        }

        return y - height
    }

    // MARK: - 4. Nearest Hospital

    private static func drawHospitalBar(y: CGFloat, callSheet: CallSheetData) -> CGFloat {
        let height: CGFloat = 18
        let rect = CGRect(x: margin, y: y - height, width: contentWidth, height: height)

        colorBorder.setStroke()
        let b = NSBezierPath(rect: rect); b.lineWidth = 1; b.stroke()

        let hospText = callSheet.nearestHospital.trimmingCharacters(in: .whitespaces)
        let full = hospText.isEmpty ? "✚ NEAREST HOSPITAL:" : "✚ NEAREST HOSPITAL: \(hospText)"
        let attr: [NSAttributedString.Key: Any] = [
            .font: fontBoldBody,
            .foregroundColor: colorBlack
        ]
        NSAttributedString(string: full, attributes: attr)
            .draw(in: CGRect(x: rect.minX + 6, y: rect.midY - 5, width: rect.width - 12, height: 11))

        return y - height - 4
    }

    // MARK: - 5. Scenes Table

    private static func drawScenesTable(y: CGFloat, shootDay: ShootDay, ensureRoom: (CGFloat) -> Void) -> CGFloat {
        var y = y
        let headerH: CGFloat = 16
        let cols: [(title: String, width: CGFloat)] = [
            ("SCENE",       65),
            ("SET / DESCRIPTION", 180),
            ("CAST",        95),
            ("PAGES",       55),
            ("LOC",         35),
            ("ADDRESS",     contentWidth - 65 - 180 - 95 - 55 - 35)
        ]

        ensureRoom(headerH + 30)

        // Draw Table Header
        let hRect = CGRect(x: margin, y: y - headerH, width: contentWidth, height: headerH)
        colorGrayHeader.setFill()
        NSBezierPath(rect: hRect).fill()

        var curX = margin
        for c in cols {
            let cell = CGRect(x: curX, y: y - headerH, width: c.width, height: headerH)
            let b = NSBezierPath(rect: cell); b.lineWidth = 0.5; colorBorder.setStroke(); b.stroke()

            let para = NSMutableParagraphStyle(); para.alignment = .center
            let attr: [NSAttributedString.Key: Any] = [.font: fontTableHdr, .foregroundColor: colorBlack, .paragraphStyle: para]
            NSAttributedString(string: c.title, attributes: attr)
                .draw(in: CGRect(x: cell.minX, y: cell.midY - 5, width: cell.width, height: 11))
            curX += c.width
        }
        y -= headerH

        let dayLocations = shootDay.callSheet.locations

        // Draw Scenes
        for scene in shootDay.scenes {
            let rowH: CGFloat = 34
            ensureRoom(rowH)

            var x = margin

            // Col 1: SCENE (# + INT/EXT/DAY)
            let c1 = CGRect(x: x, y: y - rowH, width: cols[0].width, height: rowH)
            drawCellBorder(c1)
            let numStr = scene.extractedSceneNumber
            let typeStr = "\(scene.intExtString) / \(scene.dayNightType.rawValue.uppercased())"
            drawCenteredText(numStr, font: fontBannerTitle, in: CGRect(x: c1.minX, y: c1.midY - 2, width: c1.width, height: 14))
            drawCenteredText(typeStr, font: fontSmall, in: CGRect(x: c1.minX, y: c1.minY + 3, width: c1.width, height: 10))
            x += cols[0].width

            // Col 2: SET / DESCRIPTION (Clean decorado name without INT/EXT or time suffix!)
            let c2 = CGRect(x: x, y: y - rowH, width: cols[1].width, height: rowH)
            drawCellBorder(c2)
            let decoradoStr = scene.decoradoOnly
            let synStr = scene.summary.isEmpty ? "" : scene.summary
            drawCenteredText(decoradoStr, font: fontBoldBody, in: CGRect(x: c2.minX + 4, y: c2.midY - 2, width: c2.width - 8, height: 12))
            if !synStr.isEmpty {
                drawCenteredText(synStr, font: fontSmall, in: CGRect(x: c2.minX + 4, y: c2.minY + 3, width: c2.width - 8, height: 10))
            }
            x += cols[1].width

            // Col 3: CAST
            let c3 = CGRect(x: x, y: y - rowH, width: cols[2].width, height: rowH)
            drawCellBorder(c3)
            let castStr = scene.cast.joined(separator: ", ")
            drawCenteredText(castStr, font: fontRegularBody, in: CGRect(x: c3.minX + 4, y: c3.midY - 6, width: c3.width - 8, height: 12))
            x += cols[2].width

            // Col 4: PAGES
            let c4 = CGRect(x: x, y: y - rowH, width: cols[3].width, height: rowH)
            drawCellBorder(c4)
            let pgs = formattedEighths(scene.duration)
            drawCenteredText(pgs, font: fontRegularBody, in: CGRect(x: c4.minX, y: c4.midY - 6, width: c4.width, height: 12))
            x += cols[3].width

            // Determine LOC index and Address based on scene.realLocation or scene decorado
            var locIndexStr = "1"
            var addrStr = ""
            if let matchedIdx = dayLocations.firstIndex(where: {
                (!scene.realLocation.isEmpty && $0.name.caseInsensitiveCompare(scene.realLocation) == .orderedSame) ||
                (!scene.decoradoOnly.isEmpty && $0.name.caseInsensitiveCompare(scene.decoradoOnly) == .orderedSame)
            }) {
                locIndexStr = "\(matchedIdx + 1)"
                addrStr = dayLocations[matchedIdx].address
            } else if !dayLocations.isEmpty {
                locIndexStr = "1"
                addrStr = dayLocations[0].address
            }

            // Col 5: LOC
            let c5 = CGRect(x: x, y: y - rowH, width: cols[4].width, height: rowH)
            drawCellBorder(c5)
            drawCenteredText(locIndexStr, font: fontRegularBody, in: CGRect(x: c5.minX, y: c5.midY - 6, width: c5.width, height: 12))
            x += cols[4].width

            // Col 6: ADDRESS
            let c6 = CGRect(x: x, y: y - rowH, width: cols[5].width, height: rowH)
            drawCellBorder(c6)
            drawCenteredText(addrStr, font: fontTiny, in: CGRect(x: c6.minX + 4, y: c6.midY - 10, width: c6.width - 8, height: 20))

            y -= rowH
        }

        return y - 6
    }

    // MARK: - 6. Cast Table

    private static func drawCastTable(y: CGFloat, shootDay: ShootDay, productionInfo: ProductionInfo, ensureRoom: (CGFloat) -> Void) -> CGFloat {
        var y = y
        let headerH: CGFloat = 16
        let cols: [(title: String, width: CGFloat)] = [
            ("CHARACTER",       80),
            ("ACTOR/ACTRESS",   125),
            ("STATUS",          35),
            ("PICK UP",         52),
            ("H/MU & WARDROBE", 68),
            ("ON SET",          52),
            ("WRAP",            52),
            ("LOC",             contentWidth - 80 - 125 - 35 - 52 - 68 - 52 - 52)
        ]

        ensureRoom(headerH + 20)

        // Draw Table Header
        let hRect = CGRect(x: margin, y: y - headerH, width: contentWidth, height: headerH)
        colorGrayHeader.setFill()
        NSBezierPath(rect: hRect).fill()

        var curX = margin
        for c in cols {
            let cell = CGRect(x: curX, y: y - headerH, width: c.width, height: headerH)
            let b = NSBezierPath(rect: cell); b.lineWidth = 0.5; colorBorder.setStroke(); b.stroke()

            let para = NSMutableParagraphStyle(); para.alignment = .center
            let attr: [NSAttributedString.Key: Any] = [.font: fontTableHdr, .foregroundColor: colorBlack, .paragraphStyle: para]
            NSAttributedString(string: c.title, attributes: attr)
                .draw(in: CGRect(x: cell.minX, y: cell.midY - 5, width: cell.width, height: 11))
            curX += c.width
        }
        y -= headerH

        let entries = shootDay.callSheet.castCallEntries
        for entry in entries {
            let rowH: CGFloat = 16
            ensureRoom(rowH)

            var x = margin

            let vals = [
                entry.characterName,
                entry.actorName,
                entry.ecdt,
                entry.pickupTime,
                entry.hmuWardrobeTime,
                entry.onSetTime,
                entry.wrapTime,
                entry.locationIndex
            ]

            for (i, val) in vals.enumerated() {
                let cell = CGRect(x: x, y: y - rowH, width: cols[i].width, height: rowH)
                drawCellBorder(cell)
                let isLeft = (i == 0 || i == 1)
                let para = NSMutableParagraphStyle(); para.alignment = isLeft ? .left : .center
                let attr: [NSAttributedString.Key: Any] = [
                    .font: isLeft ? fontBoldBody : fontRegularBody,
                    .foregroundColor: colorBlack,
                    .paragraphStyle: para
                ]
                let textX = isLeft ? cell.minX + 4 : cell.minX
                let textW = isLeft ? cell.width - 8 : cell.width
                NSAttributedString(string: val, attributes: attr)
                    .draw(in: CGRect(x: textX, y: cell.midY - 5, width: textW, height: 11))
                x += cols[i].width
            }
            y -= rowH
        }

        return y - 6
    }

    // MARK: - 7. Crew Call Table (Showing Name AND Role together, with name lookup)

    private static func drawCrewTable(y: CGFloat, shootDay: ShootDay, productionInfo: ProductionInfo, ensureRoom: (CGFloat) -> Void) -> CGFloat {
        var y = y
        let bannerH: CGFloat = 16
        ensureRoom(bannerH + 30)

        // Banner Header
        let bRect = CGRect(x: margin, y: y - bannerH, width: contentWidth, height: bannerH)
        colorGrayHeader.setFill()
        NSBezierPath(rect: bRect).fill()
        let b = NSBezierPath(rect: bRect); b.lineWidth = 0.5; colorBorder.setStroke(); b.stroke()

        let para = NSMutableParagraphStyle(); para.alignment = .center
        let attr: [NSAttributedString.Key: Any] = [.font: fontSectionHdr, .foregroundColor: colorBlack, .paragraphStyle: para]
        NSAttributedString(string: "CREW CALL TIMES", attributes: attr)
            .draw(in: CGRect(x: bRect.minX, y: bRect.midY - 5, width: bRect.width, height: 11))
        y -= bannerH

        let entries = shootDay.callSheet.crewCallEntries.isEmpty
            ? productionInfo.crew.map { CrewCallEntry(role: $0.role, name: $0.name, callTime: "07:30 AM", phone: $0.phone) }
            : shootDay.callSheet.crewCallEntries

        let colWidth3 = contentWidth / 3
        let rowH: CGFloat = 14

        // Group into rows of 3
        let chunks = stride(from: 0, to: entries.count, by: 3).map {
            Array(entries[$0..<min($0 + 3, entries.count)])
        }

        for chunk in chunks {
            ensureRoom(rowH)
            for (cIdx, member) in chunk.enumerated() {
                let cRect = CGRect(x: margin + CGFloat(cIdx) * colWidth3, y: y - rowH, width: colWidth3, height: rowH)
                drawCellBorder(cRect)

                let roleWidth: CGFloat = colWidth3 - 52
                let lAttr: [NSAttributedString.Key: Any] = [.font: fontRegularBody, .foregroundColor: colorBlack]

                // Look up name from productionInfo if member.name is empty
                var memberName = member.name.trimmingCharacters(in: .whitespaces)
                let memberRole = member.role.trimmingCharacters(in: .whitespaces)
                if memberName.isEmpty, !memberRole.isEmpty {
                    if let matched = productionInfo.crew.first(where: { $0.role.caseInsensitiveCompare(memberRole) == .orderedSame }) {
                        memberName = matched.name.trimmingCharacters(in: .whitespaces)
                    }
                }

                // Display label cleanly without orphaned colons
                let label: String
                if !memberRole.isEmpty && !memberName.isEmpty {
                    label = "\(memberRole): \(memberName)"
                } else if !memberRole.isEmpty {
                    label = memberRole
                } else if !memberName.isEmpty {
                    label = memberName
                } else {
                    label = "—"
                }

                NSAttributedString(string: label, attributes: lAttr)
                    .draw(in: CGRect(x: cRect.minX + 4, y: cRect.midY - 5, width: roleWidth - 6, height: 11))

                let rPara = NSMutableParagraphStyle(); rPara.alignment = .center
                let vAttr: [NSAttributedString.Key: Any] = [.font: fontBoldBody, .foregroundColor: colorBlack, .paragraphStyle: rPara]
                let timeStr = member.callTime.isEmpty ? "07:30 AM" : member.callTime
                NSAttributedString(string: timeStr, attributes: vAttr)
                    .draw(in: CGRect(x: cRect.maxX - 48, y: cRect.midY - 5, width: 46, height: 11))
            }
            y -= rowH
        }

        return y - 6
    }

    // MARK: - 8. General Notes (Unified in one single block)

    private static func drawProductionNotes(y: CGFloat, callSheet: CallSheetData, ensureRoom: (CGFloat) -> Void) -> CGFloat {
        var y = y
        let bannerH: CGFloat = 16
        ensureRoom(bannerH + 30)

        // Banner Header
        let bRect = CGRect(x: margin, y: y - bannerH, width: contentWidth, height: bannerH)
        colorGrayHeader.setFill()
        NSBezierPath(rect: bRect).fill()
        let b = NSBezierPath(rect: bRect); b.lineWidth = 0.5; colorBorder.setStroke(); b.stroke()

        let para = NSMutableParagraphStyle(); para.alignment = .center
        let attr: [NSAttributedString.Key: Any] = [.font: fontSectionHdr, .foregroundColor: colorBlack, .paragraphStyle: para]
        NSAttributedString(string: "GENERAL NOTES", attributes: attr)
            .draw(in: CGRect(x: bRect.minX, y: bRect.midY - 5, width: bRect.width, height: 11))
        y -= bannerH

        // Unified multiline text
        let notesText = callSheet.notes.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            ? callSheet.productionNotes.joined(separator: "\n")
            : callSheet.notes

        if !notesText.isEmpty {
            let paraLeft = NSMutableParagraphStyle()
            paraLeft.alignment = .left
            paraLeft.lineSpacing = 3

            let nAttr: [NSAttributedString.Key: Any] = [
                .font: fontBoldBody,
                .foregroundColor: colorDark,
                .paragraphStyle: paraLeft
            ]

            let attrString = NSAttributedString(string: notesText, attributes: nAttr)
            let textRect = attrString.boundingRect(with: CGSize(width: contentWidth - 16, height: CGFloat.greatestFiniteMagnitude), options: [.usesLineFragmentOrigin, .usesFontLeading])
            let boxHeight = max(30, textRect.height + 14)

            ensureRoom(boxHeight)
            let rRect = CGRect(x: margin, y: y - boxHeight, width: contentWidth, height: boxHeight)
            drawCellBorder(rRect)

            attrString.draw(in: CGRect(x: rRect.minX + 8, y: rRect.minY + 7, width: rRect.width - 16, height: boxHeight - 14))
            y -= boxHeight
        } else {
            let emptyH: CGFloat = 30
            ensureRoom(emptyH)
            let rRect = CGRect(x: margin, y: y - emptyH, width: contentWidth, height: emptyH)
            drawCellBorder(rRect)
            y -= emptyH
        }

        return y
    }

    // MARK: - Drawing Helpers

    private static func drawCellBorder(_ rect: CGRect) {
        let path = NSBezierPath(rect: rect)
        path.lineWidth = 0.5
        colorBorder.setStroke()
        path.stroke()
    }

    private static func drawCenteredText(_ text: String, font: NSFont, in rect: CGRect) {
        let para = NSMutableParagraphStyle(); para.alignment = .center
        para.lineBreakMode = .byTruncatingTail
        let attr: [NSAttributedString.Key: Any] = [
            .font: font,
            .foregroundColor: colorBlack,
            .paragraphStyle: para
        ]
        NSAttributedString(string: text, attributes: attr)
            .draw(in: rect)
    }
}
