using AutoDuty.Data;
using AutoDuty.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using Pictomancy;
using System.Diagnostics;
using System.Numerics;
using static AutoDuty.Managers.ContentPathsManager;
using static AutoDuty.Windows.MainWindow;

namespace AutoDuty.Windows
{
    using Dalamud.Interface;
    using Dalamud.Interface.Windowing;
    using Dalamud.Utility.Numerics;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Configurations;

    internal static class BuildTab
    {
        private static string? managedEditFile;
        private static string copyName = "";
        private static List<PathAction>? editActions;
        private static string EditFile => managedEditFile ?? Plugin.pathFile;
        private static bool EditingRunningFile => managedEditFile == null || string.Equals(managedEditFile, Plugin.pathFile, StringComparison.OrdinalIgnoreCase);
        private static List<PathAction> EditActions => EditingRunningFile ? Plugin.Actions : editActions!;
        private static bool ReadOnly => !Plugin.Recorder.Recording && PathFileStore.IsReadOnly(EditFile);

        private static void MutateEdits(Action<List<PathAction>> mutate, string reason)
        {
            if (ReadOnly) { Svc.Chat.PrintError("[AutoDuty] " + DutyToolsUi.Text("OriginalReadOnly")); return; }
            if (EditingRunningFile)
            {
                PathEditHelper.MutateActions(mutate, reason);
                if (managedEditFile != null) editActions = [.. Plugin.Actions];
            }
            else { mutate(editActions!); SavePath(); }
        }

        internal static void ResetEditorToRunning()
        {
            ActionWindow.Close();
            managedEditFile = null;
            editActions = null;
        }

        private static void SelectEditFile(string path)
        {
            ActionWindow.Close();
            managedEditFile = path;
            editActions = PathJson.ReadFile(File.ReadAllText(path)).Actions;
        }

        private static void DrawRouteFiles()
        {
            string source = EditFile;
            if (!uint.TryParse(System.Text.RegularExpressions.Regex.Match(Path.GetFileName(source), @"^\((\d+)\)").Groups[1].Value, out uint territory))
            {
                var initial = MainTab.DutySelected?.Paths.FirstOrDefault();
                if (initial == null) return;
                SelectEditFile(initial.FilePath);
                source = EditFile;
                territory = initial.container.ID;
            }
            ImGui.TextWrapped(Loc.Get("LevelingPlan.EditingFile") + ": " + source);
            var files = Directory.GetFiles(Plugin.pathsDirectory.FullName, $"({territory})*.json", SearchOption.AllDirectories)
                .Where(p => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(p), $@"^\({territory}\)\s"))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
            ImGui.SetNextItemWidth(-1);
            using (ImRaii.Disabled(Plugin.Recorder.Recording))
            if (ImGui.BeginCombo("##BuildRouteVersion", Path.GetFileName(source)))
            {
                foreach (string file in files)
                {
                    string label = Path.GetFileName(file) + " / " + DutyToolsUi.Text(PathFileStore.IsReadOnly(file) ? "OriginalVersion" : "UserVersion");
                    if (ImGui.Selectable(label, string.Equals(source, file, StringComparison.OrdinalIgnoreCase))) SelectEditFile(file);
                }
                ImGui.EndCombo();
            }
            if (ReadOnly) ImGui.TextWrapped(DutyToolsUi.Text(files.Length == 1 ? "OnlyOriginal" : "OriginalReadOnly"));
            using (ImRaii.Disabled(Plugin.Recorder.Recording))
            {
                ImGui.SetNextItemWidth(240);
                ImGui.InputText(Loc.Get("LevelingPlan.CopyName"), ref copyName, 100);
                ImGui.SameLine();
                if (ImGui.Button(Loc.Get("LevelingPlan.SaveCopy")))
                {
                    try
                    {
                        string name = string.IsNullOrWhiteSpace(copyName) ? $"Copy {DateTime.Now:yyyyMMdd-HHmmss-fff}" : copyName;
                        string destination = PathFileStore.CopyAs(EditFile, Plugin.pathsDirectory.FullName, territory, name, EditActions);
                        ConfigurationMain.Instance.DoNotUpdatePathFiles.Add(Path.GetFileName(destination));
                        ConfigurationProfileV2.Save();
                        SelectEditFile(destination);
                        saveStatus = Loc.Get("LevelingPlan.RouteSaved") + ": " + destination;
                        saveFailed = false;
                        LevelingHelper.ResetLevelingDuties();
                    }
                    catch (Exception ex) { saveFailed = true; saveStatus = ex.Message; Svc.Chat.PrintError("[AutoDuty] " + ex.Message); }
                }
            }
        }

        internal static void LoadForEditing(ContentPathsManager.DutyPath path)
        {
            if (Plugin.Recorder.Recording) return;
            SelectEditFile(path.FilePath);
            MainWindow.OpenTab("Build");
        }

        internal static void NewRecordedAction(string name)
        {
            if (ReadOnly) { Svc.Chat.PrintError("[AutoDuty] " + DutyToolsUi.Text("OriginalReadOnly")); return; }
            ClearAll();
            _action = new PathAction { Name = name, Position = Player.Available ? Player.Position : Vector3.Zero };
            if (name == "<-- Comment -->") { _comment = true; _action.Tag = ActionTag.Comment; }
            if (name == "Revival") _action.Tag = ActionTag.Revival;
            if (name == "TreasureCoffer") _action.Tag = ActionTag.Treasure;
            if (name == "SelectYesno") _action.Arguments = ["Yes"];
            if (name == "Wait") _action.Arguments = ["1000"];
            if (name is "Interactable" or "Target" or "MoveToObject" && Svc.Targets.Target is { } target)
                _action.Arguments = [target.BaseId.ToString()];
            ActionWindow.NewAction(_action);
        }

        private static PathAction? editingOriginal;
        internal static void EditStep(int index, bool fromBuild = false)
        {
            if (!fromBuild) ResetEditorToRunning();
            if (ReadOnly) { MainWindow.OpenTab("Build"); Svc.Chat.PrintError("[AutoDuty] " + DutyToolsUi.Text("OriginalReadOnly")); return; }
            if (index < 0 || index >= EditActions.Count) return;
            ClearAll();
            _addActionButton = Loc.Get("BuildTab.Modify");
            _comment = EditActions[index].Tag.HasFlag(ActionTag.Comment);
            _buildListSelected = index;
            editingOriginal = EditActions[index];
            _action = ActionWindow.EditAction(editingOriginal);
        }

        private static bool        _scrollBottom = false;
        private static string      _changelog    = string.Empty;
        private static PathAction? _action       = null;
        private static string      _actionText   = string.Empty;

        private static ActionEditWindow ActionWindow
        {
            get
            {
                if (field == null)
                    Plugin.windowSystem.AddWindow(field = new ActionEditWindow());
                return field;
            }
        }

        private static int     _buildListSelected  = -1;
        private static string  _addActionButton    = Loc.Get("BuildTab.Add"); 
        private static bool    _dragDrop           = false;
        private static bool    _comment            = false;
        private static Vector4 _argumentTextColor  = new(1, 1, 1, 1);
        private static bool    _duplicateItem      = false;
        private static int     _duplicateItemIndex = -1;

        internal static unsafe void DrawBuildTab()
        {
            SetCurrentTabName("BuildTab");
            DutyToolsUi.ExecutionStatus();
            DrawRouteFiles();
            ImGui.TextWrapped(Loc.Get("LevelingPlan.AutoSaveSteps"));
            if (saveStatus.Length > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, saveFailed ? new Vector4(1, .35f, .3f, 1) : new Vector4(.4f, 1, .6f, 1));
                ImGui.TextWrapped(saveStatus);
                ImGui.PopStyleColor();
            }
            if (ImGui.Button(DutyToolsUi.Text("Recorder"))) Plugin.ShowRecorder();
            ImGui.SameLine();
            if (ImGui.Button(DutyToolsUi.Text("CopyPath"))) Plugin.PathCommand(["path", "copy"]);
            ImGui.SameLine();
            if (ImGui.Button(DutyToolsUi.Text("PastePath"))) Plugin.PathCommand(["path", "import"]);
            if ((!InDungeon || managedEditFile != null) && !string.IsNullOrEmpty(EditFile))
            {
                using var protectOriginal = ImRaii.Disabled(ReadOnly);
                ImGui.TextWrapped(EditFile);
                if (ImGui.Button(Loc.Get("BuildTab.SavePath"))) SavePath();
                ImGui.SameLine();
                if (ImGui.Button(Loc.Get("BuildTab.AddAction"))) ImGui.OpenPopup("OfflineAddAction");
                if (ImGui.BeginPopup("OfflineAddAction"))
                {
                    foreach (var item in Plugin.actions.actionsList)
                        if (ImGui.Selectable(item.actionName)) NewRecordedAction(item.actionName);
                    ImGui.EndPopup();
                }
            }
            using (ImRaii.Disabled(ReadOnly))
            {
                if (InDungeon && managedEditFile == null)
                {
                    DrawPathElements();
                    DrawSeperator();
                    DrawButtons();
                    DrawSeperator();
                }

                DrawBuildList();
            }
        }

        private static void DrawSeperator()
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        private static void DrawPathElements()
        {
            using ImRaii.DisabledDisposable d = ImRaii.Disabled(!InDungeon || Plugin.Stage > 0 || !Player.Available);
            ImGui.Text(Loc.Get("BuildTab.BuildPath", Svc.ClientState.TerritoryType,
                               ContentHelper.DictionaryContent.TryGetValue(Svc.ClientState.TerritoryType, out Classes.Content? content) ? content.Name : TerritoryName.GetTerritoryName(Svc.ClientState.TerritoryType)));

            ImGui.TextWrapped(Path.GetFileName(EditFile));


            ImGui.AlignTextToFramePadding();
            ImGui.Text(Loc.Get("BuildTab.Changelog"));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputText("##Changelog", ref _changelog, 200);
        }

        private static void DrawButtons()
        {
            if (ImGui.Button(Loc.Get("BuildTab.AddPos")))
            {
                _scrollBottom = true;
                MutateEdits(list => list.Add(new PathAction { Name = "MoveTo", Position = Player.Position }), "add MoveTo");
            }

            ImGui.SameLine(0, 5);
            ImGuiComponents.HelpMarker(Loc.Get("BuildTab.AddPosTooltip"));
            if (ImGuiEx.ButtonWrapped(Loc.Get("BuildTab.AddAction")))
                ImGui.OpenPopup("AddActionPopup");

            ImGuiComponents.HelpMarker(Loc.Get("BuildTab.AddActionTooltip"));
            if (ImGui.BeginPopup("AddActionPopup"))
            {
                foreach ((string actionName, string actionHelp, string[] arguments) item in Plugin.actions.actionsList)
                {
                    if (item.actionName == "MoveTo")
                        continue;

                    if (ImGui.Selectable(item.actionName))
                    {
                        _buildListSelected = -1;
                        _actionText        = item.actionName;
                        _addActionButton   = Loc.Get("BuildTab.Add");
                        _comment           = item.actionName.Equals("<-- Comment -->", StringComparison.InvariantCultureIgnoreCase);

                        _action = new PathAction { Name = _actionText };

                        switch (item.actionName)
                        {
                            case "TeleportTo":
                                _action.Position = Player.Position;
                                break;
                            case "<-- Comment -->":
                                _action.Tag = ActionTag.Comment;
                                _action.Position = Vector3.Zero;
                                break;
                            case "Revival":
                                _action.Tag = ActionTag.Revival;
                                _action.Position = Vector3.Zero;
                                break;
                            case "TreasureCoffer":
                                _action.Tag = ActionTag.Treasure;
                                break;
                            case "ExitDuty":
                                _action.Position = Vector3.Zero;
                                break;
                            case "SelectYesno":
                                _action.Arguments = ["Yes"];
                                break;
                            case "MoveToObject":
                            case "Interactable":
                            case "Target":
                                IGameObject? targetObject = Player.Object?.TargetObject;
                                IGameObject? gameObject   = (targetObject ?? null) ?? ClosestObject;
                                _action.Arguments = [gameObject != null ? $"{gameObject.BaseId}" : string.Empty];
                                _action.Note      = gameObject != null ? gameObject.Name.GetText() : string.Empty;
                                break;
                            case "Boss":
                                IGameObject? gameObject2 = Player.Object?.TargetObject;
                                _action.Note = gameObject2 != null ? gameObject2.Name.GetText() : string.Empty;
                                break;
                            default:
                                break;
                        }

                        ActionWindow.NewAction(_action);
                    }

                    ImGuiComponents.HelpMarker(item.actionHelp);
                }

                ImGui.EndPopup();
            }

            ImGui.SameLine(0, 5);
            if (ImGuiEx.ButtonWrapped(Loc.Get("BuildTab.ClearPath")) && Plugin.Stage == Stage.Stopped)
            {
                MutateEdits(list => list.Clear(), "clear");
                ClearAll();
            }

            ImGuiComponents.HelpMarker(Loc.Get("BuildTab.ClearPathTooltip"));
            ImGui.SameLine(0, 5);
            if (ImGuiEx.ButtonWrapped(Loc.Get("BuildTab.SavePath"))) SavePath();

            ImGuiComponents.HelpMarker(Loc.Get("BuildTab.SavePathTooltip"));
            ImGui.SameLine(0, 5);
            if (ImGuiEx.ButtonWrapped(Loc.Get("BuildTab.LoadPath")) && Plugin.Stage == Stage.Stopped)
            {
                Plugin.LoadPath();
                ClearAll();
            }

            ImGuiComponents.HelpMarker(Loc.Get("BuildTab.LoadPathTooltip"));
            ImGui.SameLine(0, 5);
            using (ImRaii.Enabled())
            {
                using (ImRaii.Disabled(Plugin.pathFile.IsNullOrEmpty()))
                {
                    if (ImGuiEx.ButtonWrapped(Loc.Get("BuildTab.OpenFile")))
                        Process.Start("explorer", EditFile);
                }
            }
        }

        private static string saveStatus = "";
        private static bool saveFailed;

        internal static void SavePath(bool running = false)
        {
            string destination = running ? Plugin.pathFile : EditFile;
            try
            {
                PathFileStore.Save(destination, running ? Plugin.Actions : EditActions, running ? "" : _changelog, Plugin.Version);
                _changelog = string.Empty;
                ConfigurationMain.Instance.DoNotUpdatePathFiles.Add(Path.GetFileName(destination));
                ConfigurationProfileV2.Save();
                LevelingHelper.ResetLevelingDuties();
                saveFailed = false;
                saveStatus = Loc.Get("LevelingPlan.RouteSaved") + $" ({DateTime.Now:HH:mm:ss}): {Path.GetFullPath(destination)}";
                Svc.Log.Info(saveStatus);
            }
            catch (Exception ex)
            {
                saveFailed = true;
                saveStatus = Loc.Get("LevelingPlan.RouteSaveFailed") + $": {destination}\n{ex.Message}";
                Svc.Log.Error(ex.ToString());
                Svc.Chat.PrintError("[AutoDuty] " + saveStatus);
            }
        }

        private static void DrawBuildList()
        {
            if (!ImGui.BeginListBox("##BuildList", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y)))
                return;

            try
            {
                if (InDungeon || !string.IsNullOrEmpty(Plugin.pathFile))
                {
                    int? dragIndex = null;
                    int? dragNext  = null;


                    foreach ((PathAction Value, int Index) item in EditActions.Select((value, index) => (Value: value, Index: index)))
                    {
                        bool editing = ActionWindow.IsOpen && ReferenceEquals(editingOriginal, item.Value);
                        Vector4 v4 = item.Value.Name.StartsWith("<--", StringComparison.InvariantCultureIgnoreCase) ? new Vector4(0, 255, 0, 1) : new Vector4(255, 255, 255, 1);

                        ImGui.PushStyleColor(ImGuiCol.Text, v4);
                        if (editing)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.15f, .4f, .65f, .8f));
                            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.2f, .5f, .75f, .9f));
                            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(.2f, .5f, .75f, 1f));
                        }

                        // Track the edited object, not its index: inserting or reordering
                        // other steps must not move the highlight to a different action.
                        if (ImGui.Selectable($"{(editing ? "> " : "")}{item.Index}: ###Text{item.Index}", editing || item.Index == _buildListSelected))
                            EditStep(item.Index, fromBuild: true);
                        if (editing) ImGui.PopStyleColor(3);
                        bool openStepMenu = ImGui.IsItemClicked(ImGuiMouseButton.Right);

                        if (ImGui.IsItemClicked(ImGuiMouseButton.Middle))
                        {
                            _duplicateItem      = true;
                            _duplicateItemIndex = item.Index;
                        }

                        if (ImGui.IsItemActive() && !ImGui.IsItemHovered() && !_dragDrop && !ActionWindow.IsOpen)
                            _buildListSelected = item.Index;

                        if (_buildListSelected == item.Index && ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ActionWindow.IsOpen)
                        {
                            float mouseYDelta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left).Y;

                            if (MathF.Abs(mouseYDelta) > ImGui.GetTextLineHeight())
                            {
                                _dragDrop = true;
                                dragIndex = item.Index;
                                dragNext  = item.Index + (mouseYDelta < 0f ? -1 : 1);
                            }
                        }

                        foreach ((Vector4 color, string text) in item.Value.GetCustomText(item.Index))
                        {
                            ImGui.SameLine(0, 0);
                            ImGui.TextColored(color, text);
                            openStepMenu |= ImGui.IsItemClicked(ImGuiMouseButton.Right);
                        }

                        if (openStepMenu) ImGui.OpenPopup("BuildStepContext" + item.Index);
                        if (ImGui.BeginPopup("BuildStepContext" + item.Index))
                        {
                            DutyToolsUi.TeleportMenuItem(item.Value);
                            ImGui.EndPopup();
                        }

                        ImGui.PopStyleColor();
                    }

                    if (dragIndex.HasValue && dragNext is >= 0 && dragNext < EditActions.Count)
                    {
                        MutateEdits(list => (list[dragNext.Value], list[dragIndex.Value]) = (list[dragIndex.Value], list[dragNext.Value]), "reorder");

                        _buildListSelected = dragNext.Value;
                        ImGui.ResetMouseDragDelta();
                    }
                    else if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ActionWindow.IsOpen)
                    {
                        _dragDrop          = false;
                        _buildListSelected = -1;
                    }

                    if (_duplicateItem)
                    {
                        PathAction clone = EditActions[_duplicateItemIndex].JSONClone(ConfigurationMain.JsonSerializerSettings);
                        if (clone != null)
                            MutateEdits(list => list.Insert(_duplicateItemIndex, clone), "duplicate");
                        _duplicateItem = false;
                    }
                }
                else
                {
                    ImGuiEx.TextWrapped(new Vector4(0, 1, 0, 1), Loc.Get("BuildTab.NotInDungeonMessage"));
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
            }

            if (_scrollBottom)
            {
                ImGui.SetScrollHereY(1.0f);
                _scrollBottom = false;
            }

            ImGui.EndListBox();
        }

        internal static void DeleteStep(PathAction? target)
        {
            int index = PathEditHelper.FindReference(EditActions, target);
            if (index < 0)
            {
                int runningIndex = PathEditHelper.FindReference(Plugin.Actions, target);
                if (runningIndex >= 0) PathEditHelper.MutateActions(list => list.RemoveAt(runningIndex), "delete");
                return;
            }
            MutateEdits(list => list.RemoveAt(index), "delete");
            if (ReferenceEquals(target, editingOriginal))
            {
                ActionWindow.Close();
                ClearAll();
            }
        }

        private static void ClearAll()
        {
            editingOriginal = null;
            _actionText        = string.Empty;
            _addActionButton   = Loc.Get("BuildTab.Add");
            _buildListSelected = -1;
            _action            = null;
            _comment           = false;
        }

        private static void AddAction()
        {
            if (_action == null)
                return;

            if(_comment)
            {
                _action.Position   = Vector3.Zero;
                if (!_action.Note.StartsWith("<--") && !_action.Note.EndsWith("-->"))
                    _action.Note = $"<-- {_action.Note} -->";
            }
            
            if (_buildListSelected == -1)
            {
                MutateEdits(list => list.Add(_action), "add action");
                _scrollBottom = true;
            }
            else
            {
                int index = editingOriginal == null ? _buildListSelected : EditActions.IndexOf(editingOriginal);
                if (index < 0) { Svc.Chat.PrintError("[AutoDuty] Edited step was deleted; reopen the editor."); return; }
                MutateEdits(list => list[index] = _action, "replace action");
            }
            ActionWindow.Close();

            ClearAll();
        }

        public static void DrawHelper(PctDrawList drawList)
        {
            ActionWindow.DrawHelper(drawList);
        }

        public class ActionEditWindow : Window
        {
            private static readonly ActionTag[] ACTIONTAG_SELECTION = [ActionTag.None, ActionTag.Synced, ActionTag.Unsynced, ActionTag.W2W, ActionTag.Treasure];

            private PathAction? action;

            private (string actionName, string actionHelp, string[] arguments) actionDefinition;

            public ActionEditWindow() : base($"Add/Edit Action###AddActionUI")
            {
                // Full-width inputs depend on the current content width. Auto-resizing this
                // window feeds that width back into its next frame and makes it keep growing.
                this.Flags = ImGuiWindowFlags.NoTitleBar;
                this.Size = new Vector2(760, 620);
                this.SizeCondition = ImGuiCond.Appearing;

                this.SizeConstraints = new WindowSizeConstraints
                                       {
                                           MinimumSize = new Vector2(620, 240),
                                           MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
                                       };
            }

            public void NewAction(PathAction newAction)
            {
                this.action = newAction;

                this.WindowName       = $"Add Action {this.action.Name}###AddActionUI";
                this.actionDefinition = Plugin.actions.actionsList.FirstOrDefault(a => a.actionName == this.action.Name);
                
                this.RequestFocus = true;

                this.IsOpen = true;
            }

            public ref PathAction EditAction(PathAction originalAction)
            {
                this.action = originalAction.JSONClone(ConfigurationMain.JsonSerializerSettings);

                this.WindowName    = $"Edit Action {this.action.Name}###AddActionUI";
                this.actionDefinition = Plugin.actions.actionsList.FirstOrDefault(a => a.actionName == this.action.Name);

                this.RequestFocus = true;

                this.IsOpen = true;
                return ref this.action!;
            }

            public void Close()
            {
                this.action = null;
                this.IsOpen = false;
                ClearAll();

                this.RequestFocus = false;

                this.WindowName = "Add/Edit Action###AddActionUI";
            }

            public override void Draw()
            {
                if (this.action == null)
                    return;

                using var readOnlyScope = ImRaii.Disabled(ReadOnly);

                using (ImRaii.Disabled(this.action.Arguments.Count == 0 && this.actionDefinition.arguments.Length != 0 && !_comment))
                {
                    if (ImGuiEx.ButtonWrapped(_addActionButton))
                    {
                        if (this.action.Name is "MoveToObject" or "Target" or "Interactable")
                        {
                            if (uint.TryParse(this.action.Arguments[0], out uint _))
                                AddAction();
                            else
                                ShowPopup(Loc.Get("BuildTab.ErrorTitle"), Loc.Get("BuildTab.DataIdError", this.action.Name), true);
                        }
                        else
                        {
                            AddAction();
                        }
                        return;
                    }
                }

                ImGui.SameLine();
                using (ImRaii.Disabled(_buildListSelected < 0))
                {
                    if (ImGuiEx.ButtonWrapped(Loc.Get("BuildTab.Delete")))
                    {
                        DeleteStep(editingOriginal);
                        return;
                    }

                    ImGui.SameLine();
                    if (ImGuiEx.ButtonWrapped(Loc.Get("BuildTab.CopyToClipboard")))
                        ImGui.SetClipboardText(this.action.ToCustomString());
                    if (Plugin.isDev)
                    {
                        ImGui.SameLine();
                        using (ImRaii.Disabled(!Player.Available))
                        {
                            unsafe
                            {
                                if (ImGuiEx.ButtonWrapped(Loc.Get("BuildTab.TeleportTo")))
                                    Player.GameObject->SetPosition(this.action!.Position.X, this.action.Position.Y, this.action.Position.Z);
                            }
                        }
                    }
                }

                float closeWidth = ImGui.CalcTextSize("X").X + ImGui.GetStyle().FramePadding.X * 2;
                float closeX = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - closeWidth;
                ImGui.SameLine(Math.Max(ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().ItemSpacing.X, closeX));
                if (ImGui.Button("X"))
                {
                    this.Close();
                    return;
                }

                if (!(this.actionDefinition.arguments.Length <= 0 || _comment))
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextColored(_argumentTextColor, Loc.Get("BuildTab.Arguments"));

                    ImGui.SameLine();
                    float addX = ImGui.GetCursorPosX();
                    if (ImGui.Button(Loc.Get("BuildTab.Plus") + "##AddArgument"))
                        this.action.Arguments.Add(string.Empty);



                    for (int i = 0; i < this.action.Arguments.Count; i++)
                    {
                        ImGui.PushID(i);

                        using (ImRaii.Disabled(i <= 0))
                        {
                            if (ImGui.Button(Loc.Get("BuildTab.Up") + "##MoveUp"))
                                (this.action.Arguments[i], this.action.Arguments[i - 1]) = (this.action.Arguments[i - 1], this.action.Arguments[i]);
                        }

                        ImGui.SameLine();
                        using (ImRaii.Disabled(i >= this.action.Arguments.Count - 1))
                        {
                            if (ImGui.Button(Loc.Get("BuildTab.Down") + "##MoveDown"))
                                (this.action.Arguments[i], this.action.Arguments[i + 1]) = (this.action.Arguments[i + 1], this.action.Arguments[i]);
                        }

                        ImGui.SameLine();
                        using (ImRaii.Disabled(!ImGui.GetIO().KeyCtrl))
                        {
                            if (ImGui.Button(Loc.Get("BuildTab.Remove") + "##RemoveArgument"))
                            {
                                this.action.Arguments.RemoveAt(i);
                                i--;
                                if (i == -1)
                                    return;
                            }
                        }

                        ImGui.SameLine();
                        ImGui.SetCursorPosX(addX);
                        if (ImGui.Button(Loc.Get("BuildTab.Plus") + "##AddArgument"))
                            this.action.Arguments.Insert(i + 1, string.Empty);
                        ImGui.SameLine();

                        if (this.actionDefinition.arguments.Length > i)
                        {
                            ImGui.SameLine();
                            ImGui.TextColored(_argumentTextColor, this.actionDefinition.arguments[i]);
                            ImGui.SameLine();
                        }

                        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                        string tempArgument = this.action.Arguments[i];
                        if (ImGui.InputText($"##Argument{i}", ref tempArgument, 200))
                            this.action.Arguments[i] = tempArgument;
                        ImGui.PopID();
                    }

                    ImGui.Spacing();
                }


                if (!_comment)
                {
                    if (ImGui.Button(Loc.Get("BuildTab.Position")))
                        this.action.Position = (this.action.Position - Player.Position).LengthSquared() <= 0.1f ? Vector3.Zero : Player.Position;

                    ImGui.SameLine();
                    ImGui.PushItemWidth(145 * ImGuiHelpers.GlobalScale);
                    //ImGui.InputText("##Position", ref _positionText, 200);

                    Vector3 actionPosition = this.action.Position;
                    ImGui.InputFloat("X##PositionX", ref actionPosition.X, 0.1f, 1f);
                    ImGui.SameLine();
                    ImGui.InputFloat("Y##PositionY", ref actionPosition.Y, 0.1f, 1f);
                    ImGui.SameLine();
                    ImGui.InputFloat("Z##PositionZ", ref actionPosition.Z, 0.1f, 1f);
                    ImGui.PopItemWidth();

                    this.action.Position = actionPosition;
                }

                bool strict = this.action.Flags.HasFlag(PathActionFlags.StrictConditions);
                if (ImGui.Checkbox(DutyToolsUi.Text("StrictConditions"), ref strict))
                    this.action.Flags = strict ? this.action.Flags | PathActionFlags.StrictConditions : this.action.Flags & ~PathActionFlags.StrictConditions;
                ImGui.AlignTextToFramePadding();
                ImGui.Text(Loc.Get("BuildTab.Note"));
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                string actionNote = this.action.Note;
                ImGui.InputText("##Note", ref actionNote, 200);
                this.action.Note = actionNote;

                using (ImRaii.Disabled(this.action.Tag.HasAnyFlag(ActionTag.Comment, ActionTag.Revival) || this.action.Name == "TreasureCoffer"))
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(Loc.Get("BuildTab.Tag"));
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.BeginCombo("##TagSelection", this.action.Tag.HasAnyFlag(ActionTag.None, ActionTag.Synced, ActionTag.Unsynced) ? this.action.Tag.ToCustomString() : ActionTag.None.ToCustomString()))
                    {
                        foreach (ActionTag actionTag in ACTIONTAG_SELECTION)
                        {
                            bool selected = this.action.Tag.HasFlag(actionTag);
                            if (ImGui.Selectable(actionTag.ToCustomString(), selected))
                                if (selected)
                                    this.action.Tag &= ~actionTag;
                                else
                                    this.action.Tag |= actionTag;
                        }

                        ImGui.EndCombo();
                    }
                }

                if (!_comment)
                {
                    bool noPartyCoherency = this.action.Flags.HasFlag(PathActionFlags.NoPartyCoherency);
                    if(ImGui.Checkbox(Loc.Get("BuildTab.NoPartyCoherency"), ref noPartyCoherency))
                        if(noPartyCoherency)
                            this.action.Flags = this.action.Flags |= PathActionFlags.NoPartyCoherency;
                        else
                            this.action.Flags = this.action.Flags &= ~PathActionFlags.NoPartyCoherency;


                    ImGui.Separator();
                    ImGui.Text(Loc.Get("BuildTab.Conditions"));
                    DrawPathConditions(this.action.Conditions, "Before");

                    ImGui.Separator();
                    ImGui.Text(Loc.Get("BuildTab.ConditionsWhile"));
                    DrawPathConditions(this.action.ConditionsWhile, "While");

                    ImGui.Separator();
                    ImGui.Text(Loc.Get("BuildTab.ConditionsUntil"));
                    DrawPathConditions(this.action.ConditionsUntil, "Until");
                }

                return;

                static void DrawPathConditions(List<PathActionCondition> conditions, string id)
                {
                    using ImRaii.IdDisposable _ = ImRaii.PushId($"BuildTab_Condition_{id}");

                    int delIndex = -1;
                    for (int index = 0; index < conditions.Count; index++)
                    {
                        using ImRaii.IdDisposable __ = ImRaii.PushId($"_{index}");

                        PathActionCondition condition = conditions[index];
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.TrashAlt))
                            delIndex = index;
                        ImGui.SameLine();
                        ImGui.AlignTextToFramePadding();
                        ImGui.Text(condition.ParseKey.ToLocalizedString());
                        ImGui.SameLine();
                        float indent = ImGui.GetCursorPosX();
                        ImGui.Indent(indent);
                        condition.DrawConfig();
                        ImGui.Unindent(indent);
                        ImGui.Separator();
                    }

                    if (delIndex >= 0)
                        conditions.RemoveAt(delIndex);


                    PathActionCondition? actionCondition = PathActionCondition.ConditionSelection();
                    if (actionCondition != null)
                        conditions.Add(actionCondition);
                }
            }

            public void DrawHelper(PctDrawList drawList)
            {
                if (this.IsOpen && this.action?.Position.LengthSquared() > 0.1f)
                {
                    drawList.AddCircle(this.action.Position, 1.25f, 0xFFFFFFFF, thickness: 2);
                    drawList.AddText(this.action.Position + Vector3.UnitZ * 1.1f, 0xFFFFFFFF, Loc.Get("BuildTab.PlusZ"), 1f);
                    drawList.AddText(this.action.Position + Vector3.UnitZ * -1.1f, 0xFFFFFFFF, Loc.Get("BuildTab.MinusZ"), 1f);

                    drawList.AddText(this.action.Position + Vector3.UnitX * 1.1f, 0xFFFFFFFF, Loc.Get("BuildTab.PlusX"), 1f);
                    drawList.AddText(this.action.Position + Vector3.UnitX * -1.1f, 0xFFFFFFFF, Loc.Get("BuildTab.MinusX"), 1f);

                    if (PlayerHelper.IsValid)
                    {
                        float playerY = Player.Position.Y;
                        float ydiff = (this.action.Position.Y - playerY);
                        Vector3 posWithY = this.action.Position.WithY(playerY);
                        if (MathF.Abs(ydiff) > 0.1f)
                        {
                            drawList.AddText(this.action.Position + Vector3.UnitY * MathF.Sign(ydiff), 0xFFFFFFFF, Loc.Get("BuildTab.YDiff") + ydiff.ToString("F3", CultureInfo.CurrentCulture), 1f);

                            drawList.PathLineTo(this.action.Position);
                            drawList.PathLineTo(posWithY);
                            drawList.PathStroke(0xFFFFFFFF);
                        }

                        float playerX = Player.Position.X;
                        float xdiff = (this.action.Position.X - playerX);
                        if (MathF.Abs(xdiff) > 0.1f)
                        {
                            drawList.AddText(posWithY + Vector3.UnitY / 2 + Vector3.UnitX * MathF.Sign(xdiff) * -1, 0xFFFFFFFF, Loc.Get("BuildTab.XDiff") + ydiff.ToString("F3", CultureInfo.CurrentCulture), 1f);

                            drawList.PathLineTo(posWithY);
                            drawList.PathLineTo(posWithY.WithX(playerX));
                            drawList.PathStroke(0xFFFFFFFF);
                        }

                        float playerZ = Player.Position.Z;
                        float zdiff = (this.action.Position.Z - playerZ);
                        if (MathF.Abs(zdiff) > 0.1f)
                        {
                            drawList.AddText(posWithY + Vector3.UnitY / 2 + Vector3.UnitZ * MathF.Sign(zdiff) * -1, 0xFFFFFFFF, Loc.Get("BuildTab.ZDiff") + zdiff.ToString("F3", CultureInfo.CurrentCulture), 1f);

                            drawList.PathLineTo(posWithY);
                            drawList.PathLineTo(posWithY.WithZ(playerZ));
                            drawList.PathStroke(0xFFFFFFFF);
                        }

                        Vector3 diff = (this.action.Position - Player.Position);
                        float diffL = diff.Length();
                        if (MathF.Abs(diffL) > 0.1f)
                        {
                            drawList.PathLineTo(this.action.Position);
                            drawList.PathLineTo(Player.Position);
                            drawList.PathStroke(0xFFFFFFFF);

                            drawList.AddText(this.action.Position - Vector3.Normalize(diff), 0xFF00FF00, Loc.Get("BuildTab.Diff") + diffL.ToString("F3", CultureInfo.CurrentCulture), 1f);
                        }
                    }

                    drawList.PathLineTo(this.action.Position - Vector3.UnitX);
                    drawList.PathLineTo(this.action.Position + Vector3.UnitX);
                    drawList.PathStroke(0xFFFFFFFF);
                    drawList.PathLineTo(this.action.Position - Vector3.UnitZ);
                    drawList.PathLineTo(this.action.Position + Vector3.UnitZ);
                    drawList.PathStroke(0xFFFFFFFF);
                }
            }
        }
    }
}
