// CalendarView.swift
// Calendar grid with drag-and-drop scene scheduling

import SwiftUI
import UniformTypeIdentifiers
import AppKit

// MARK: - DropIndicatorView & Delegates

struct DropIndicatorView: View {
    var body: some View {
        Rectangle()
            .fill(Color.blue)
            .frame(height: 2)
            .padding(.vertical, 1)
    }
}

struct CombinedDayDropDelegate: DropDelegate {
    let dayId: UUID
    let scenes: [Scene]
    @Binding var dropTargetDayId: UUID?
    @Binding var dropTargetPosition: Int?
    @Binding var dayDropTargetId: UUID?
    @Binding var draggingDayId: UUID?
    let onSceneDrop: (UUID) -> Void
    let onDayDrop: (UUID) -> Void

    func performDrop(info: DropInfo) -> Bool {
        if let dayIdStr = draggingDayId, dayIdStr != dayId {
            onDayDrop(dayIdStr)
            draggingDayId = nil
            dayDropTargetId = nil
            return true
        }
        guard let item = info.itemProviders(for: [UTType.text.identifier]).first else { return false }
        item.loadItem(forTypeIdentifier: UTType.text.identifier, options: nil) { data, _ in
            if let data = data as? Data, let idStr = String(data: data, encoding: .utf8), let uuid = UUID(uuidString: idStr) {
                DispatchQueue.main.async {
                    onSceneDrop(uuid)
                }
            }
        }
        return true
    }

    func dropEntered(info: DropInfo) {
        if draggingDayId != nil && draggingDayId != dayId {
            dayDropTargetId = dayId
        } else {
            dropTargetDayId = dayId
            dropTargetPosition = scenes.count
        }
    }

    func dropExited(info: DropInfo) {
        if dayDropTargetId == dayId { dayDropTargetId = nil }
        if dropTargetDayId == dayId { dropTargetDayId = nil }
    }
}

struct SceneDropDelegate: DropDelegate {
    let dayId: UUID
    let position: Int
    @Binding var dropTargetDayId: UUID?
    @Binding var dropTargetPosition: Int?
    let onDrop: (UUID) -> Void

    func performDrop(info: DropInfo) -> Bool {
        guard let item = info.itemProviders(for: [UTType.text.identifier]).first else { return false }
        item.loadItem(forTypeIdentifier: UTType.text.identifier, options: nil) { data, _ in
            if let data = data as? Data, let idStr = String(data: data, encoding: .utf8), let uuid = UUID(uuidString: idStr) {
                DispatchQueue.main.async {
                    onDrop(uuid)
                }
            }
        }
        return true
    }

    func dropEntered(info: DropInfo) {
        dropTargetDayId = dayId
        dropTargetPosition = position
    }

    func dropExited(info: DropInfo) {
        if dropTargetDayId == dayId && dropTargetPosition == position {
            dropTargetDayId = nil
            dropTargetPosition = nil
        }
    }
}

// MARK: - Standalone DayCellView

struct DayCellView: View {
    let day: ShootDay
    let dayIndex: Int
    let dayNumber: Int?
    let isSidebarCollapsed: Bool
    let selectedSceneIDs: Set<UUID>
    let conflictSceneIDs: Set<UUID>
    let duplicateSceneNumberIDs: Set<UUID>

    @Binding var draggingDayId: UUID?
    @Binding var dropTargetDayId: UUID?
    @Binding var dropTargetPosition: Int?
    @Binding var dayDropTargetId: UUID?
    @Binding var addingEventForDayId: UUID?
    @Binding var callSheetDay: ShootDay?
    @Binding var interactingSceneId: UUID?
    @Binding var draggedSceneId: UUID?

    let onEditScene: (Int, Scene) -> Void
    let onRemoveScene: (Scene) -> Void
    let onDuplicateScene: (Scene) -> Void
    let onSelectScene: (Scene) -> Void
    let onSendToDay: (Scene) -> Void
    let onHandleSceneDrop: (UUID, Int) -> Void
    let onHandleDayRearrange: (UUID) -> Void
    let onToggleBlackout: (ShootDay) -> Void
    let onToggleBlackoutWeekday: (ShootDay) -> Void

    @Environment(\.colorScheme) private var colorScheme

    private var visibleScenes: [Scene] {
        day.scenes.filter { !$0.isBanner || $0.isCalendarEvent }
    }

    private var isTarget: Bool {
        dayDropTargetId == day.id || dropTargetDayId == day.id
    }

    private var borderColor: Color {
        if dayDropTargetId == day.id { return .green }
        if dropTargetDayId == day.id { return .red }
        if day.isBlackout { return .red.opacity(0.4) }
        return .primary.opacity(0.12)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            header
            sceneList
            Spacer()
            footer
        }
        .padding(8)
        .frame(minHeight: 120, alignment: .topLeading)
        .background(
            ZStack {
                Color(NSColor.controlBackgroundColor)
                if isWeekend(day.date) { Color.black.opacity(colorScheme == .dark ? 0.2 : 0.05) }
                if day.isBlackout { Color.red.opacity(colorScheme == .dark ? 0.25 : 0.1) }
            }
        )
        .cornerRadius(8)
        .overlay(
            RoundedRectangle(cornerRadius: 8)
                .stroke(borderColor, lineWidth: isTarget ? 2 : 1)
        )
        .opacity(draggingDayId == day.id ? 0.4 : 1.0)
        .onDrop(of: [UTType.text.identifier], delegate: CombinedDayDropDelegate(
            dayId: day.id,
            scenes: visibleScenes,
            dropTargetDayId: $dropTargetDayId,
            dropTargetPosition: $dropTargetPosition,
            dayDropTargetId: $dayDropTargetId,
            draggingDayId: $draggingDayId,
            onSceneDrop: { sceneId in onHandleSceneDrop(sceneId, visibleScenes.count) },
            onDayDrop: { sourceDayId in onHandleDayRearrange(sourceDayId) }
        ))
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(spacing: 4) {
                Image(systemName: "line.3.horizontal")
                    .font(.system(size: 9, weight: .medium))
                    .foregroundColor(draggingDayId == day.id ? .blue : .secondary)
                    .padding(2)
                    .contentShape(Rectangle())
                    .onDrag {
                        draggingDayId = day.id
                        return NSItemProvider(object: "day:\(day.id.uuidString)" as NSString)
                    }
                    .help("Drag to move this day's scenes and call sheet to another date")

                Button {
                    callSheetDay = day
                } label: {
                    HStack(spacing: 3) {
                        Text(formattedDate(day.date))
                            .font(.caption).bold()
                            .foregroundColor(day.isBlackout ? .red : .primary)
                            .lineLimit(1)
                        if day.hasCallSheetData {
                            Circle()
                                .fill(Color.blue)
                                .frame(width: 5, height: 5)
                        }
                    }
                }
                .buttonStyle(.plain)

                Spacer(minLength: 2)

                if let dayNumber {
                    Text("\(L("Day")) \(dayNumber)")
                        .font(.caption2).fontWeight(.semibold).foregroundColor(.secondary)
                }

                Button {
                    addingEventForDayId = day.id
                } label: {
                    Image(systemName: "plus.circle")
                        .font(.system(size: 10, weight: .medium))
                        .foregroundColor(.secondary)
                }
                .buttonStyle(.plain)
                .help(L("Add Calendar Event"))
            }

            if !day.callSheet.lunchTime.isEmpty || !day.callSheet.snackTime.isEmpty || !day.callSheet.dinnerTime.isEmpty {
                HStack(spacing: 4) {
                    Spacer()
                    if !day.callSheet.lunchTime.isEmpty {
                        Text("🍽️ \(day.callSheet.lunchTime)")
                            .font(.system(size: 8))
                            .foregroundColor(.secondary)
                            .lineLimit(1)
                    }
                    if !day.callSheet.snackTime.isEmpty {
                        Text("☕ \(day.callSheet.snackTime)")
                            .font(.system(size: 8))
                            .foregroundColor(.secondary)
                            .lineLimit(1)
                    }
                    if !day.callSheet.dinnerTime.isEmpty {
                        Text("🎬 \(day.callSheet.dinnerTime)")
                            .font(.system(size: 8))
                            .foregroundColor(.secondary)
                            .lineLimit(1)
                    }
                }
            }
        }
        .contextMenu {
            Button(L("Add Calendar Event")) { addingEventForDayId = day.id }
            Button(day.isBlackout ? "Mark as Available" : "Mark as Unavailable") { onToggleBlackout(day) }
            Button(day.isBlackout ? "Mark Weekday Available" : "Mark Weekday Unavailable") { onToggleBlackoutWeekday(day) }
        }
    }

    private var sceneList: some View {
        VStack(spacing: 2) {
            ForEach(Array(visibleScenes.enumerated()), id: \.element.id) { sceneIndex, scene in
                VStack(spacing: 0) {
                    if dropTargetDayId == day.id && dropTargetPosition == sceneIndex {
                        DropIndicatorView()
                    }
                    SceneCardView(
                        scene: scene,
                        dayId: day.id,
                        dayIndex: dayIndex,
                        sceneIndex: sceneIndex,
                        interactingSceneId: $interactingSceneId,
                        isSelected: selectedSceneIDs.contains(scene.id),
                        selectionCount: selectedSceneIDs.count,
                        showCast: isSidebarCollapsed,
                        hasConflict: conflictSceneIDs.contains(scene.id),
                        hasDuplicateSceneNumber: duplicateSceneNumberIDs.contains(scene.id),
                        isOnBlackoutDay: day.isBlackout,
                        onEdit:      { onEditScene(sceneIndex, scene) },
                        onRemove:    { onRemoveScene(scene) },
                        onDuplicate: { onDuplicateScene(scene) },
                        onDragStart: { draggedSceneId = scene.id },
                        onDragEnd:   { draggedSceneId = nil },
                        onSelect:    { onSelectScene(scene) },
                        onSendToDay: { onSendToDay(scene) },
                        dragPayload: { scene.id.uuidString }
                    )
                }
                .onDrop(of: [UTType.text.identifier], delegate: SceneDropDelegate(
                    dayId: day.id,
                    position: sceneIndex,
                    dropTargetDayId: $dropTargetDayId,
                    dropTargetPosition: $dropTargetPosition,
                    onDrop: { sceneId in onHandleSceneDrop(sceneId, sceneIndex) }
                ))
            }

            if dropTargetDayId == day.id && dropTargetPosition == visibleScenes.count {
                DropIndicatorView()
            }
        }
    }

    @ViewBuilder
    private var footer: some View {
        if !visibleScenes.isEmpty {
            let scriptScenes = visibleScenes.filter { !$0.isBanner }
            let totalDur = scriptScenes.reduce(0) { $0 + $1.duration }
            let totalEst = scriptScenes.reduce(0) { $0 + $1.estimatedTime }
            if totalDur > 0 || totalEst > 0 {
                Text("\(L("Total:")) \(formattedEighths(totalDur)) · \(formattedTime(totalEst))")
                    .font(.caption2).fontWeight(.medium).foregroundColor(.secondary)
            }
        }
    }

    private func formattedDate(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "EEE MMM d"
        return formatter.string(from: date)
    }
}

// MARK: - CompactMonthCalendarView

struct CompactMonthCalendarView: View {
    @Environment(\.colorScheme) private var colorScheme
    @ObservedObject private var l10n = LocalizationManager.shared
    @Binding var shootDays: [ShootDay]
    let assignScene:  (Scene, ShootDay) -> Void
    @Binding var allScenes: [Scene]
    let updateScene:  (Scene, UUID) -> Void
    let removeScene:  (Scene, UUID) -> Void
    let projectTitle: String
    let productionInfo: ProductionInfo
    let isSidebarCollapsed: Bool
    @Binding var selectedSceneIDs:    Set<UUID>
    @Binding var lastSelectedSceneID: UUID?
    let conflictDates: Set<Date>
    let conflictSceneIDs: Set<UUID>
    let duplicateSceneNumberIDs: Set<UUID>
    let scheduleLockChangedDates: Set<Date>
    @Binding var scrollToDate: Date?
    let onBeforeSceneChange: () -> Void
    let onSceneChanged: () -> Void
    let onCallSheetExport: (ShootDay) -> Void

    // Editing state
    @State private var editingScene:      Scene?
    @State private var editingDayId:      UUID?
    @State private var editingDayIndex:   Int?
    @State private var editingSceneIndex: Int?
    @State private var showingEditSheet = false

    // Call sheet state
    @State private var callSheetDay: ShootDay? = nil

    // Send to Day state
    @State private var showingSendToDaySheet = false
    @State private var sendToDaySceneIDs: [UUID] = []

    // Day rearrange drag/drop state
    @State private var draggingDayId:       UUID? = nil
    @State private var dayDropTargetId:     UUID? = nil

    // Drag/drop state
    @State private var dropTargetDayId:    UUID?
    @State private var dropTargetPosition: Int?
    @State private var draggedSceneId:     UUID?
    @State private var interactingSceneId: UUID?

    // Add Calendar Event sheet state
    @State private var addingEventForDayId: UUID? = nil
    @State private var editingEventScene: Scene? = nil
    @State private var editingEventDayId: UUID? = nil

    private var weekdaySymbols: [String] {
        let isSpanish = LocalizationManager.shared.currentLanguage == .spanish
        if isSpanish {
            return ["LUNES", "MARTES", "MIÉRCOLES", "JUEVES", "VIERNES", "SÁBADO", "DOMINGO"]
        } else {
            return ["MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"]
        }
    }

    private var leadingOffsetCount: Int {
        guard let firstDay = shootDays.first else { return 0 }
        let weekday = Calendar.current.component(.weekday, from: firstDay.date) // 1=Sun, 2=Mon, 3=Tue, 4=Wed, 5=Thu, 6=Fri, 7=Sat
        return (weekday + 5) % 7
    }

    private var weekdayHeaderRow: some View {
        HStack(spacing: 8) {
            ForEach(weekdaySymbols, id: \.self) { symbol in
                Text(symbol)
                    .font(.system(size: 11, weight: .bold))
                    .foregroundColor(.secondary)
                    .frame(maxWidth: .infinity)
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 8)
        .background(Color.gray.opacity(colorScheme == .dark ? 0.2 : 0.08))
        .cornerRadius(6)
        .padding(.horizontal, 16)
        .padding(.bottom, 4)
    }

    var body: some View {
        VStack(spacing: 0) {
            weekdayHeaderRow

            ScrollViewReader { proxy in
                ScrollView(.vertical, showsIndicators: true) {
                    let columns = Array(repeating: GridItem(.flexible(minimum: 100), spacing: 8), count: 7)
                    LazyVGrid(columns: columns, spacing: 8) {
                        ForEach(0..<leadingOffsetCount, id: \.self) { _ in
                            Color.clear
                                .frame(minHeight: 120)
                        }
                        ForEach(Array(shootDays.enumerated()), id: \.element.id) { dayIndex, day in
                            dayCell(day: day, dayIndex: dayIndex)
                                .id(day.id)
                        }
                    }
                    .padding(.horizontal, 16)
                    .padding(.bottom, 60)
                }
                .onChange(of: scrollToDate) { newValue in
                    guard let date = newValue else { return }
                    if let target = shootDays.first(where: { Calendar.current.isDate($0.date, inSameDayAs: date) }) {
                        withAnimation { proxy.scrollTo(target.id, anchor: .top) }
                    }
                    scrollToDate = nil
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .tooltipContainer()
        .sheet(isPresented: $showingEditSheet) {
            editSheetContent()
        }
        .sheet(item: $callSheetDay) { day in
            callSheetEditorContent(for: day)
        }
        .sheet(isPresented: $showingSendToDaySheet) {
            SendToDaySheet(
                shootDays:  shootDays,
                sceneCount: sendToDaySceneIDs.count,
                onSelect: { targetDayId in
                    sendScenes(sendToDaySceneIDs, toDay: targetDayId)
                    showingSendToDaySheet = false
                },
                onCancel: { showingSendToDaySheet = false }
            )
        }
        .sheet(isPresented: Binding(
            get: { addingEventForDayId != nil },
            set: { if !$0 { addingEventForDayId = nil } }
        )) {
            CalendarEventInputSheet(isPresented: Binding(
                get: { addingEventForDayId != nil },
                set: { if !$0 { addingEventForDayId = nil } }
            ), onSave: { newEvent in
                if let targetId = addingEventForDayId,
                   let idx = shootDays.firstIndex(where: { $0.id == targetId }) {
                    shootDays[idx].scenes.append(newEvent)
                    onSceneChanged()
                }
            })
        }
        .sheet(item: $editingEventScene) { ev in
            CalendarEventInputSheet(
                isPresented: Binding(
                    get: { editingEventScene != nil },
                    set: { if !$0 { editingEventScene = nil; editingEventDayId = nil } }
                ),
                initialEvent: ev,
                onSave: { updatedEvent in
                    if let dId = editingEventDayId ?? shootDays.first(where: { $0.scenes.contains(where: { $0.id == ev.id }) })?.id,
                       let dayIdx = shootDays.firstIndex(where: { $0.id == dId }),
                       let sceneIdx = shootDays[dayIdx].scenes.firstIndex(where: { $0.id == ev.id }) {
                        shootDays[dayIdx].scenes[sceneIdx] = updatedEvent
                        onSceneChanged()
                    }
                    editingEventScene = nil
                    editingEventDayId = nil
                }
            )
        }
        .onChange(of: showingEditSheet) { isShowing in
            if !isShowing { clearEditingState() }
        }
    }

    // MARK: - Day Cell Component

    private var dayNumbers: [UUID: Int] { productionDayNumbers(for: shootDays) }

    @ViewBuilder
    private func dayCell(day: ShootDay, dayIndex: Int) -> some View {
        DayCellView(
            day: day,
            dayIndex: dayIndex,
            dayNumber: dayNumbers[day.id],
            isSidebarCollapsed: isSidebarCollapsed,
            selectedSceneIDs: selectedSceneIDs,
            conflictSceneIDs: conflictSceneIDs,
            duplicateSceneNumberIDs: duplicateSceneNumberIDs,
            draggingDayId: $draggingDayId,
            dropTargetDayId: $dropTargetDayId,
            dropTargetPosition: $dropTargetPosition,
            dayDropTargetId: $dayDropTargetId,
            addingEventForDayId: $addingEventForDayId,
            callSheetDay: $callSheetDay,
            interactingSceneId: $interactingSceneId,
            draggedSceneId: $draggedSceneId,
            onEditScene: { sceneIndex, scene in editScene(dayIndex: dayIndex, sceneIndex: sceneIndex, scene: scene, dayId: day.id) },
            onRemoveScene: { scene in removeFromDay(scene, dayId: day.id) },
            onDuplicateScene: { scene in duplicateScene(scene) },
            onSelectScene: { scene in selectScene(scene, dayId: day.id) },
            onSendToDay: { scene in beginSendToDay(scene) },
            onHandleSceneDrop: { sceneId, pos in handleSceneDrop(sceneId: sceneId, targetDayId: day.id, targetPosition: pos) },
            onHandleDayRearrange: { sourceDayId in handleDayRearrange(sourceDayId: sourceDayId, targetDayId: day.id) },
            onToggleBlackout: { targetDay in toggleBlackout(targetDay) },
            onToggleBlackoutWeekday: { targetDay in toggleBlackoutForWeekday(targetDay) }
        )
    }

    // MARK: - Actions & Handlers

    private func editScene(dayIndex: Int, sceneIndex: Int, scene: Scene, dayId: UUID) {
        if scene.isCalendarEvent {
            editingEventScene = scene
            editingEventDayId = dayId
            return
        }
        var tempScene = scene
        tempScene.autoExtractSceneNumberIfNeeded()
        editingScene      = tempScene
        editingDayId      = dayId
        editingDayIndex   = dayIndex
        editingSceneIndex = sceneIndex
        showingEditSheet  = true
    }

    private func saveCurrentSceneEdit() {
        guard let editingScene, let editingDayId else { return }
        onBeforeSceneChange()
        updateScene(editingScene, editingDayId)
    }

    private func clearEditingState() {
        editingScene      = nil
        editingDayId      = nil
        editingDayIndex   = nil
        editingSceneIndex = nil
    }

    private func removeFromDay(_ scene: Scene, dayId: UUID) {
        onBeforeSceneChange()
        removeScene(scene, dayId)
    }

    private func duplicateScene(_ scene: Scene) {
        guard let dayId = shootDays.first(where: { $0.scenes.contains(where: { $0.id == scene.id }) })?.id else { return }
        var dup = scene
        dup.id = UUID()
        onBeforeSceneChange()
        assignScene(dup, shootDays.first(where: { $0.id == dayId })!)
    }

    private func selectScene(_ scene: Scene, dayId: UUID) {
        if NSEvent.modifierFlags.contains(.command) {
            if selectedSceneIDs.contains(scene.id) {
                selectedSceneIDs.remove(scene.id)
            } else {
                selectedSceneIDs.insert(scene.id)
            }
            lastSelectedSceneID = scene.id
        } else if NSEvent.modifierFlags.contains(.shift), let lastID = lastSelectedSceneID {
            let dayScenes = shootDays.first(where: { $0.id == dayId })?.scenes ?? []
            if let lastIdx = dayScenes.firstIndex(where: { $0.id == lastID }),
               let currentIdx = dayScenes.firstIndex(where: { $0.id == scene.id }) {
                let start = min(lastIdx, currentIdx)
                let end = max(lastIdx, currentIdx)
                for idx in start...end {
                    selectedSceneIDs.insert(dayScenes[idx].id)
                }
            } else {
                selectedSceneIDs = [scene.id]
                lastSelectedSceneID = scene.id
            }
        } else {
            selectedSceneIDs = [scene.id]
            lastSelectedSceneID = scene.id
        }
    }

    private func beginSendToDay(_ scene: Scene) {
        if selectedSceneIDs.contains(scene.id) && selectedSceneIDs.count > 1 {
            sendToDaySceneIDs = Array(selectedSceneIDs)
        } else {
            sendToDaySceneIDs = [scene.id]
        }
        showingSendToDaySheet = true
    }

    private func sendScenes(_ ids: [UUID], toDay targetDayId: UUID) {
        onBeforeSceneChange()
        guard let targetDay = shootDays.first(where: { $0.id == targetDayId }) else { return }
        var scenesToMove: [Scene] = []
        for dIdx in 0..<shootDays.count {
            let matches = shootDays[dIdx].scenes.filter { ids.contains($0.id) }
            scenesToMove.append(contentsOf: matches)
            shootDays[dIdx].scenes.removeAll { ids.contains($0.id) }
        }
        for s in scenesToMove {
            assignScene(s, targetDay)
        }
    }

    private func handleSceneDrop(sceneId: UUID, targetDayId: UUID, targetPosition: Int) {
        onBeforeSceneChange()
        guard let sourceDayIndex = shootDays.firstIndex(where: { $0.scenes.contains(where: { $0.id == sceneId }) }),
              let targetDayIndex = shootDays.firstIndex(where: { $0.id == targetDayId }) else { return }

        let idsToMove: [UUID] = selectedSceneIDs.contains(sceneId) && selectedSceneIDs.count > 1
            ? Array(selectedSceneIDs)
            : [sceneId]

        var scenesToMove: [Scene] = []
        if sourceDayIndex == targetDayIndex {
            let dayScenes = shootDays[sourceDayIndex].scenes
            let movingSet = Set(idsToMove)
            scenesToMove = dayScenes.filter { movingSet.contains($0.id) }
            var remaining = dayScenes.filter { !movingSet.contains($0.id) }
            let clampedPos = min(targetPosition, remaining.count)
            remaining.insert(contentsOf: scenesToMove, at: clampedPos)
            shootDays[sourceDayIndex].scenes = remaining
        } else {
            for dIdx in 0..<shootDays.count {
                let matches = shootDays[dIdx].scenes.filter { idsToMove.contains($0.id) }
                scenesToMove.append(contentsOf: matches)
                shootDays[dIdx].scenes.removeAll { idsToMove.contains($0.id) }
            }
            let clampedPos = min(targetPosition, shootDays[targetDayIndex].scenes.count)
            shootDays[targetDayIndex].scenes.insert(contentsOf: scenesToMove, at: clampedPos)
        }
        onSceneChanged()
    }

    private func handleDayRearrange(sourceDayId: UUID, targetDayId: UUID) {
        onBeforeSceneChange()
        guard let srcIdx = shootDays.firstIndex(where: { $0.id == sourceDayId }),
              let dstIdx = shootDays.firstIndex(where: { $0.id == targetDayId }),
              srcIdx != dstIdx else { return }
        shootDays.swapAt(srcIdx, dstIdx)
        onSceneChanged()
    }

    private func toggleBlackout(_ day: ShootDay) {
        onBeforeSceneChange()
        if let idx = shootDays.firstIndex(where: { $0.id == day.id }) {
            shootDays[idx].isBlackout.toggle()
            onSceneChanged()
        }
    }

    private func toggleBlackoutForWeekday(_ day: ShootDay) {
        onBeforeSceneChange()
        let targetWeekday = Calendar.current.component(.weekday, from: day.date)
        let newValue = !day.isBlackout
        for idx in 0..<shootDays.count {
            if Calendar.current.component(.weekday, from: shootDays[idx].date) == targetWeekday {
                shootDays[idx].isBlackout = newValue
            }
        }
        onSceneChanged()
    }

    // MARK: - Sheets

    @ViewBuilder
    private func editSheetContent() -> some View {
        if let editingDayIndex, let editingSceneIndex,
           editingDayIndex < shootDays.count,
           editingSceneIndex < shootDays[editingDayIndex].scenes.count {
            SceneEditSheet(
                scene: $shootDays[editingDayIndex].scenes[editingSceneIndex],
                isPresented: $showingEditSheet,
                onSave: { saveCurrentSceneEdit() },
                onDelete: {
                    if let dId = editingDayId, let editingScene {
                        removeFromDay(editingScene, dayId: dId)
                    }
                    showingEditSheet = false
                },
                canGoPrevious: editingSceneIndex > 0,
                canGoNext: editingSceneIndex < shootDays[editingDayIndex].scenes.count - 1,
                onPrevious: {
                    if editingSceneIndex > 0 {
                        editScene(dayIndex: editingDayIndex, sceneIndex: editingSceneIndex - 1, scene: shootDays[editingDayIndex].scenes[editingSceneIndex - 1], dayId: shootDays[editingDayIndex].id)
                    }
                },
                onNext: {
                    if editingSceneIndex < shootDays[editingDayIndex].scenes.count - 1 {
                        editScene(dayIndex: editingDayIndex, sceneIndex: editingSceneIndex + 1, scene: shootDays[editingDayIndex].scenes[editingSceneIndex + 1], dayId: shootDays[editingDayIndex].id)
                    }
                },
                positionLabel: "Day \(editingDayIndex + 1), Scene \(editingSceneIndex + 1)"
            )
        }
    }

    @ViewBuilder
    private func callSheetEditorContent(for day: ShootDay) -> some View {
        if let idx = shootDays.firstIndex(where: { $0.id == day.id }) {
            CallSheetEditor(
                shootDay: $shootDays[idx],
                productionInfo: productionInfo,
                isPresented: Binding(
                    get: { callSheetDay != nil },
                    set: { if !$0 { callSheetDay = nil } }
                ),
                onSave: {
                    onSceneChanged()
                },
                onExportPDF: { exportedDay in
                    callSheetDay = nil
                    onCallSheetExport(exportedDay)
                },
                dayNumber: dayNumbers[day.id],
                totalProductionDays: shootDays.count
            )
        }
    }
}

// MARK: - SceneCardView (Compact single-line horizontal strip for Calendar View)

struct SceneCardView: View {
    let scene: Scene
    let dayId: UUID
    let dayIndex: Int
    let sceneIndex: Int
    @Binding var interactingSceneId: UUID?
    let isSelected:     Bool
    let selectionCount: Int
    let showCast:       Bool
    let hasConflict:    Bool
    let hasDuplicateSceneNumber: Bool
    let isOnBlackoutDay: Bool
    let onEdit:      () -> Void
    let onRemove:    () -> Void
    let onDuplicate: () -> Void
    let onDragStart: () -> Void
    let onDragEnd:   () -> Void
    let onSelect:    () -> Void
    let onSendToDay: () -> Void
    let dragPayload: () -> String

    private var isDragging: Bool { interactingSceneId == scene.id }
    private var isFlagged: Bool { hasConflict || isOnBlackoutDay }
    private var displayColor: Color {
        if scene.isCalendarEvent {
            return Color(hex: scene.bannerColorHex.isEmpty ? "6366F1" : scene.bannerColorHex)
        }
        return isFlagged ? .red : scene.stripColor
    }

    var body: some View {
        Group {
            if scene.isCalendarEvent {
                HStack(spacing: 4) {
                    Image(systemName: "calendar")
                        .font(.system(size: 8, weight: .bold))
                        .foregroundColor(displayColor)
                    let clockStr = scene.customStartTime.isEmpty ? scene.summary : scene.customStartTime
                    if !clockStr.isEmpty {
                        Text(clockStr)
                            .font(.system(size: 8.5, weight: .bold))
                            .foregroundColor(displayColor)
                    }
                    Text(scene.bannerTitle.isEmpty ? scene.title : scene.bannerTitle)
                        .font(.system(size: 9.5, weight: .semibold))
                        .foregroundColor(.primary)
                        .lineLimit(1)
                    Spacer(minLength: 0)
                }
                .padding(.horizontal, 6)
                .padding(.vertical, 3)
                .background(displayColor.opacity(0.15))
                .cornerRadius(4)
                .overlay(
                    RoundedRectangle(cornerRadius: 4)
                        .stroke(displayColor.opacity(0.4), lineWidth: 1)
                )
            } else {
                HStack(spacing: 4) {
                    let numStr = scene.sceneNumber.isEmpty ? "" : "\(scene.sceneNumber). "
                    Text("\(numStr)\(scene.title)")
                        .font(.system(size: 9.5, weight: .semibold))
                        .foregroundColor(scene.stripTextColor)
                        .lineLimit(1)
                    Spacer(minLength: 2)
                    if hasConflict || isOnBlackoutDay {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .font(.system(size: 7))
                            .foregroundColor(.red)
                    }
                    if scene.duration > 0 {
                        Text(formattedEighths(scene.duration))
                            .font(.system(size: 8, weight: .bold))
                            .foregroundColor(scene.stripTextColor.opacity(0.8))
                    }
                }
                .padding(.horizontal, 6)
                .padding(.vertical, 3.5)
                .background(displayColor)
                .cornerRadius(4)
                .overlay(
                    RoundedRectangle(cornerRadius: 4)
                        .stroke(isSelected ? Color.accentColor : Color.black.opacity(0.15), lineWidth: isSelected ? 2 : 0.5)
                )
            }
        }
        .onTapGesture { onSelect() }
        .simultaneousGesture(TapGesture(count: 2).onEnded { onEdit() })
        .contextMenu {
            Button("Edit Scene") { onEdit() }
            Button("Duplicate Scene") { onDuplicate() }
            Button("Move to Day...") { onSendToDay() }
            Divider()
            Button("Remove from Day") { onRemove() }
        }
        .onDrag {
            onDragStart()
            return NSItemProvider(object: dragPayload() as NSString)
        }
        .fastTooltip(scene.tooltipText)
    }
}
