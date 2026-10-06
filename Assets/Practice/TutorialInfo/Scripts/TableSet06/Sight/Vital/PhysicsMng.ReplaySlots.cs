using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        public const int ReplaySlotCount = 20;
        const int ReplaySlotsPerRow = 10;
        bool replayLoadMode, replayLineBeforeLoad, replaySavePanelOpen;
        int selectedReplaySlot;
        readonly bool[] replaySlotFilled = new bool[ReplaySlotCount];
        Button[] replaySlotButtons;
        Button replaySave, replayLoad;
        string replaySlotMessage = "";
        TMP_InputField replayTitle;
        int replayTitleSlot = -1;
        string replayTitleSaved = "";
        bool matchArchiveSaveMode;
        System.Action<int, string, string> matchArchiveSaveAction;
        System.Action matchArchiveSaveClosed;
        string matchArchiveDefaultTitle = "";
        string ReplayTitlePath(int slot) => Path.Combine(ReplaySlotDirectory, $"slot{slot + 1:00}.title.txt");

        public string GetReplaySlotTitle(int slot)
        {
            if (slot < 0 || slot >= ReplaySlotCount) return "";
            try
            {
                string path = ReplayTitlePath(slot);
                return File.Exists(path) ? NormalizeReplayTitle(File.ReadAllText(path)) : "";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { replaySlotMessage = "제목을 읽을 수 없습니다"; return ""; }
        }
        static string NormalizeReplayTitle(string value)
        {
            value = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length > 40 ? value.Substring(0, 40) : value;
        }
        public bool SetReplaySlotTitle(int slot, string title)
        {
            if (slot < 0 || slot >= ReplaySlotCount) return false;
            title = NormalizeReplayTitle(title);
            try
            {
                Directory.CreateDirectory(ReplaySlotDirectory);
                string path = ReplayTitlePath(slot), temp = path + ".tmp";
                File.WriteAllText(temp, title, System.Text.Encoding.UTF8);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                if (replayTitle && replayTitleSlot == slot)
                { replayTitleSaved = title; replayTitle.SetTextWithoutNotify(title); }
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { replaySlotMessage = "제목 저장 실패 · 저장 공간과 권한을 확인하세요"; return false; }
        }
        void CommitReplayTitle()
        {
            if (matchArchiveSaveMode) return;
            if (replayTitle && replayTitleSlot >= 0 && replayTitle.text != replayTitleSaved)
                SetReplaySlotTitle(replayTitleSlot, replayTitle.text);
        }
        void UpdateReplayTitle()
        {
            if (!replayTitle)
            {
                var obj = new GameObject("Replay Title", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
                obj.transform.SetParent(replayPanel, false);
                var rect = (RectTransform)obj.transform;
                rect.anchoredPosition = IsMatchReplayBrowser ? new Vector2(-173, 48) : matchArchiveSaveMode ? new Vector2(0, 105) : new Vector2(170, 48);
                rect.sizeDelta = IsMatchReplayBrowser ? new Vector2(210, 32) : matchArchiveSaveMode ? new Vector2(420, 32) : new Vector2(330, 32);
                var background = obj.GetComponent<Image>(); background.color = new Color(.08f, .17f, .2f, .95f);
                replayTitle = obj.GetComponent<TMP_InputField>();
                replayTitle.targetGraphic = background;
                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                viewport.SetParent(rect, false); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
                viewport.offsetMin = new Vector2(8, 2); viewport.offsetMax = new Vector2(-8, -2);
                replayTitle.textViewport = viewport;
                var text = CreateReplayTitleText(viewport, "Text");
                var placeholder = CreateReplayTitleText(viewport, "Placeholder");
                placeholder.text = "제목 입력 (자동 저장)"; placeholder.color = new Color(.7f, .75f, .78f);
                replayTitle.textComponent = text; replayTitle.placeholder = placeholder;
                // Keep IME composition plain too; TMP otherwise inserts visible <u> tags.
                replayTitle.richText = false;
                replayTitle.characterLimit = 40; replayTitle.lineType = TMP_InputField.LineType.SingleLine;
                replayTitle.onEndEdit.AddListener(_ => CommitReplayTitle());
            }
            if (replayTitleSlot != selectedReplaySlot)
            {
                // Commit against the previous slot even when a slot click ends text editing.
                CommitReplayTitle(); replayTitleSlot = selectedReplaySlot;
                replayTitleSaved = matchArchiveSaveMode ? matchArchiveDefaultTitle : GetReplaySlotTitle(replayTitleSlot);
                replayTitle.SetTextWithoutNotify(replayTitleSaved);
            }
            replayTitle.interactable = ReplaySlotReady && replayTitleSlot >= 0;
        }
        TMP_Text CreateReplayTitleText(RectTransform parent, string name)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = undoButton.GetComponentInChildren<TextMeshProUGUI>(true).font;
            text.fontSize = 19; text.alignment = TextAlignmentOptions.MidlineLeft;
            text.richText = false; text.raycastTarget = false;
            return text;
        }
        List<ReplayFrame> replayBeforeLoad;
        float replayStepBeforeLoad;
        int replayCueBeforeLoad;
        ReplayStroke replayStrokeBeforeLoad;
        public bool ReplayLoadMode => replayLoadMode;
        public string ReplaySlotDirectory => string.IsNullOrEmpty(selectedReplayFolder) ? ReplayRootDirectory : Path.Combine(ReplayRootDirectory, "Folders", selectedReplayFolder);
        public string MatchReplaySummary
        {
            get
            {
                if (selectedReplaySlot < 0) return "슬롯을 선택하세요";
                try
                {
                    var info = new FileInfo(ReplaySlotPath(selectedReplaySlot));
                    if (!info.Exists) return "빈 슬롯";
                    double kb = info.Length / 1024.0;
                    return $"용량 {kb:0.#} KB  ·  날짜 {info.LastWriteTime:yy.MM.dd HH:mm}";
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                { return "기록 정보를 읽을 수 없습니다"; }
            }
        }
        string ReplaySlotPath(int slot) => Path.Combine(ReplaySlotDirectory, $"slot{slot + 1:00}.bin");
        bool ReplaySlotReady => matchArchiveSaveMode
            ? OnlineAdapter != null && OnlineAdapter.CanSaveMatchReplay
            : IsLocalPractice && !IsBallPlacement && !recordingShot && !inMove && !scatterPending && !shotController.IsShotAnimating;

        public bool OpenMatchReplaySavePanel(System.Action<int, string, string> saveAction, System.Action closed, string defaultTitle)
        {
            if (OnlineAdapter == null || !OnlineAdapter.CanSaveMatchReplay || saveAction == null ||
                !replayPanel || replaySlotButtons == null || !replaySave || !replayTitle) return false;
            matchArchiveSaveMode = true;
            matchArchiveSaveAction = saveAction; matchArchiveSaveClosed = closed;
            matchArchiveDefaultTitle = defaultTitle ?? "";
            selectedReplayFolder = ""; selectedReplaySlot = -1; replayTitleSlot = -1;
            replayTitleSaved = ""; replaySlotMessage = "폴더와 저장 슬롯을 선택하세요";
            replayLoadMode = false; replaySavePanelOpen = true;
            RefreshReplaySlots(); RefreshReplayFolderButtons();
            if (replayPanel)
            {
                ApplyMatchArchiveSaveLayout();
                replayPanel.gameObject.SetActive(true);
                replayPanel.SetAsLastSibling();
            }
            return true;
        }

        public void CloseMatchReplaySavePanel()
        {
            if (!matchArchiveSaveMode) return;
            replaySavePanelOpen = false; matchArchiveSaveMode = false;
            matchArchiveSaveAction = null; matchArchiveDefaultTitle = "";
            selectedReplayFolder = ""; selectedReplaySlot = -1; replayTitleSlot = -1;
            if (replayPanel) replayPanel.gameObject.SetActive(false);
            var closed = matchArchiveSaveClosed; matchArchiveSaveClosed = null;
            closed?.Invoke();
        }

        void SaveSelectedReplay()
        {
            if (matchArchiveSaveMode)
            {
                if (ReplaySlotReady && selectedReplaySlot >= 0)
                    matchArchiveSaveAction?.Invoke(selectedReplaySlot, ReplaySlotDirectory,
                        replayTitle ? replayTitle.text : matchArchiveDefaultTitle);
                return;
            }
            SaveReplaySlot(selectedReplaySlot);
        }

        public void SetReplayPanelMessage(string message) { replaySlotMessage = message ?? ""; }

        public void ExitPracticeReplay()
        {
            CommitReplayTitle();
            if (replayFolderDialog) replayFolderDialog.SetActive(false);
            StopReplayView();
            replaySavePanelOpen = false;
            if (replayPanel) replayPanel.gameObject.SetActive(false);
            if (replayLoadMode)
            {
                replayFrames.Clear();
                if (replayBeforeLoad != null) replayFrames.AddRange(replayBeforeLoad);
                replayStep = replayStepBeforeLoad; replayCueId = replayCueBeforeLoad;
                replayStroke = replayStrokeBeforeLoad;
                line.enabled = replayLineBeforeLoad;
                replayBeforeLoad = null; replayLoadMode = false; ReplayFrameIndex = 0;
            }
            replaySlotMessage = "";
        }
        void OpenReplaySavePanel()
        {
            if (!ReplaySlotReady || IsReplayBrowser) return;
            CommitReplayTitle();
            InstallBundledReplaySlots();
            replayTitleSlot = -1;
            UpdateReplayTitle();
            replaySavePanelOpen = true;
            RefreshReplaySlots();
            replaySlotMessage = CanReplayShot
                ? "저장할 번호와 제목을 선택한 뒤 저장을 누르세요"
                : "샷을 진행한 뒤 기록을 저장할 수 있습니다";
            if (replayPanel)
            {
                replayPanel.SetAsLastSibling();
                replayPanel.gameObject.SetActive(true);
            }
        }
        public void OpenReplaySlots()
        {
            if (!ReplaySlotReady || replayLoadMode) return;
            ClearCuePreparation();
            InstallBundledReplaySlots();
            StopReplayView();
            replayLineBeforeLoad = line.enabled; line.enabled = false;
            replayBeforeLoad = new List<ReplayFrame>(replayFrames);
            replayStepBeforeLoad = replayStep; replayCueBeforeLoad = replayCueId;
            replayStrokeBeforeLoad = replayStroke;
            replayStroke = default;
            replayFrames.Clear(); ReplayFrameIndex = 0;
            replayLoadMode = true; selectedReplaySlot = -1;
            replaySlotMessage = "불러오기 · 저장된 번호를 선택하세요";
            RefreshReplaySlots();
        }
        public void SelectReplaySlot(int slot)
        {
            if (slot < 0 || slot >= ReplaySlotCount || !ReplaySlotReady) return;
            selectedReplaySlot = slot;
            if (replayLoadMode)
            {
                if (LoadReplaySlot(slot) && IsReplayBrowser)
                    TogglePracticeReplay();
            }
            else replaySlotMessage = $"{slot + 1:00}번 선택 · " + (replaySlotFilled[slot] ? "저장하면 기존 기록을 덮어씁니다" : "빈 슬롯");
        }
        void RefreshReplaySlots()
        {
            for (int i = 0; i < ReplaySlotCount; i++) replaySlotFilled[i] = File.Exists(ReplaySlotPath(i));
        }

        void InstallBundledReplaySlots()
        {
            // Seed this project's slots once. Existing user recordings always win.
            string marker = Path.Combine(ReplayRootDirectory, ".renewal-imported");
            if (File.Exists(marker)) return;
            try
            {
                Directory.CreateDirectory(ReplayRootDirectory);
                for (int i = 0; i < ReplaySlotCount; i++)
                {
                    if (File.Exists(Path.Combine(ReplayRootDirectory, $"slot{i + 1:00}.bin"))) continue;
                    var data = Resources.Load<TextAsset>($"PracticeReplaySeeds/slot{i + 1:00}");
                    if (!data) continue;
                    File.WriteAllBytes(Path.Combine(ReplayRootDirectory, $"slot{i + 1:00}.bin"), data.bytes);
                    var title = Resources.Load<TextAsset>($"PracticeReplaySeeds/slot{i + 1:00}.title");
                    if (title && !File.Exists(Path.Combine(ReplayRootDirectory, $"slot{i + 1:00}.title.txt"))) File.WriteAllText(Path.Combine(ReplayRootDirectory, $"slot{i + 1:00}.title.txt"), title.text);
                }
                File.WriteAllText(marker, "1");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { replaySlotMessage = "기존 기록을 가져오지 못했습니다 · 저장 권한을 확인하세요"; }
        }
        public bool SaveReplaySlot(int slot)
        {
            if (slot < 0 || slot >= ReplaySlotCount || replayLoadMode || !CanReplayShot) return false;
            CommitReplayTitle();
            string path = ReplaySlotPath(slot), temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(ReplaySlotDirectory);
                using (var writer = new BinaryWriter(File.Create(temp)))
                {
                    writer.Write(0x4D525031); writer.Write(3); writer.Write(ballcs.Length);
                    writer.Write(replayCueId); writer.Write(replayStep); writer.Write(replayFrames.Count);
                    writer.Write(replayStroke.available);
                    if (replayStroke.available)
                    {
                        writer.Write(replayStroke.power); writer.Write(replayStroke.follow);
                        writer.Write(replayStroke.point.x); writer.Write(replayStroke.point.y);
                        var rotation = replayStroke.cueRotation;
                        writer.Write(rotation.x); writer.Write(rotation.y); writer.Write(rotation.z); writer.Write(rotation.w);
                    }
                    foreach (var frame in replayFrames)
                    {
                        writer.Write(frame.time);
                        for (int i = 0; i < ballcs.Length; i++)
                        {
                            WriteReplayVector(writer, frame.positions[i]);
                            var q = frame.rotations[i]; writer.Write(q.x); writer.Write(q.y); writer.Write(q.z); writer.Write(q.w);
                            WriteReplayVector(writer, frame.velocities[i]); WriteReplayVector(writer, frame.spins[i]);
                        }
                        writer.Write(frame.cueRecorded); writer.Write(frame.cueVisible);
                        WriteReplayVector(writer, frame.cuePosition);
                        var cueRotation = frame.cueRotation;
                        writer.Write(cueRotation.x); writer.Write(cueRotation.y); writer.Write(cueRotation.z); writer.Write(cueRotation.w);
                        WriteReplayVector(writer, frame.cueContact); WriteReplayVector(writer, frame.cueSlide);
                    }
                }
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                selectedReplaySlot = slot; RefreshReplaySlots();
                replaySlotMessage = $"{slot + 1:00}번 저장 완료"; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { replaySlotMessage = "저장 실패 · 저장 공간과 파일 권한을 확인하세요"; return false; }
        }
        static void WriteReplayVector(BinaryWriter writer, Vector3 v) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
        static float ReadReplayFloat(BinaryReader reader)
        {
            float v = reader.ReadSingle();
            if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException();
            return v;
        }
        static Vector3 ReadReplayVector(BinaryReader reader) => new Vector3(ReadReplayFloat(reader), ReadReplayFloat(reader), ReadReplayFloat(reader));
        public bool LoadReplaySlot(int slot)
        {
            if (slot < 0 || slot >= ReplaySlotCount || !replayLoadMode || !ReplaySlotReady) return false;
            try
            {
                var frames = new List<ReplayFrame>(); int cue; float step;
                ReplayStroke stroke = default;
                using (var reader = new BinaryReader(File.OpenRead(ReplaySlotPath(slot))))
                {
                    if (reader.ReadInt32() != 0x4D525031) throw new InvalidDataException();
                    int version = reader.ReadInt32();
                    if (version < 1 || version > 3) throw new InvalidDataException();
                    int recordedBalls = reader.ReadInt32();
                    if (recordedBalls != ballcs.Length && !(recordedBalls == 3 && ballcs.Length == 4)) throw new InvalidDataException();
                    cue = reader.ReadInt32(); step = ReadReplayFloat(reader); int count = reader.ReadInt32();
                    if (version >= 2 && reader.ReadBoolean())
                    {
                        stroke.available = true;
                        stroke.power = ReadReplayFloat(reader); stroke.follow = ReadReplayFloat(reader);
                        stroke.point = new Vector2(ReadReplayFloat(reader), ReadReplayFloat(reader));
                        stroke.cueRotation = new Quaternion(ReadReplayFloat(reader), ReadReplayFloat(reader), ReadReplayFloat(reader), ReadReplayFloat(reader));
                        var q = stroke.cueRotation;
                        float norm = q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w;
                        if (stroke.power < 0 || stroke.power > 1 || stroke.follow < 0 || stroke.follow > 1 ||
                            stroke.point.sqrMagnitude > 371f * 371f || norm < .9f || norm > 1.1f) throw new InvalidDataException();
                    }
                    if (cue < 0 || cue >= recordedBalls || step <= 0 || step > 1 || count < 2 || count > MaxReplayFrames ||
                        reader.BaseStream.Length != reader.BaseStream.Position + count * (4L + 52L * recordedBalls + (version >= 3 ? 54L : 0))) throw new InvalidDataException();
                    for (int f = 0; f < count; f++)
                    {
                        var frame = new ReplayFrame { time = ReadReplayFloat(reader), positions = new Vector3[recordedBalls],
                            rotations = new Quaternion[recordedBalls], velocities = new Vector3[recordedBalls], spins = new Vector3[recordedBalls] };
                        if (Mathf.Abs(frame.time - f * step) > .01f) throw new InvalidDataException();
                        for (int i = 0; i < recordedBalls; i++)
                        {
                            frame.positions[i] = ReadReplayVector(reader);
                            var q = new Quaternion(ReadReplayFloat(reader), ReadReplayFloat(reader), ReadReplayFloat(reader), ReadReplayFloat(reader));
                            float norm = q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w;
                            if (norm < .9f || norm > 1.1f || frame.positions[i].sqrMagnitude > 1000000f) throw new InvalidDataException();
                            frame.rotations[i] = q;
                            frame.velocities[i] = ReadReplayVector(reader); frame.spins[i] = ReadReplayVector(reader);
                        }
                        if (version >= 3)
                        {
                            frame.cueRecorded = reader.ReadBoolean(); frame.cueVisible = reader.ReadBoolean();
                            frame.cuePosition = ReadReplayVector(reader);
                            frame.cueRotation = new Quaternion(ReadReplayFloat(reader), ReadReplayFloat(reader), ReadReplayFloat(reader), ReadReplayFloat(reader));
                            frame.cueContact = ReadReplayVector(reader); frame.cueSlide = ReadReplayVector(reader);
                            var q = frame.cueRotation;
                            float norm = q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w;
                            if (frame.cueRecorded && (norm < .9f || norm > 1.1f || frame.cuePosition.sqrMagnitude > 1000000f ||
                                frame.cueContact.sqrMagnitude > 1000000f || frame.cueSlide.sqrMagnitude > 1000000f)) throw new InvalidDataException();
                        }
                        frames.Add(frame);
                    }
                }
                StopReplayView(); replayFrames.Clear(); replayFrames.AddRange(frames);
                replayStroke = stroke;
                replayCueId = cue; replayStep = step; selectedReplaySlot = slot; replaySlotMessage = "";
                ResetPracticeReplay(); return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidDataException)
            { replaySlotMessage = "빈 슬롯이거나 읽을 수 없는 기록입니다"; return false; }
        }
        void UpdateReplaySlots()
        {
            if (replaySlotButtons == null)
            {
                replaySlotButtons = new Button[ReplaySlotCount];
                for (int i = 0; i < ReplaySlotCount; i++)
                {
                    int slot = i;
                    var button = ReplayButton($"Replay Slot {i + 1:00}", $"{i + 1:00}", 0, () => SelectReplaySlot(slot));
                    float rowY = matchArchiveSaveMode ? 32 : 6;
                    float rowGap = IsMatchReplayBrowser ? 42 : 42;
                    var rect = (RectTransform)button.transform; rect.anchoredPosition = new Vector2(-300 + (i % ReplaySlotsPerRow) * 68, rowY - (i / ReplaySlotsPerRow) * rowGap); rect.sizeDelta = new Vector2(60, 34);
                    button.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta = rect.sizeDelta;
                    replaySlotButtons[i] = button;
                }
                replaySave = ReplayButton("Replay Save", "저장", 0, SaveSelectedReplay);
                var saveRect = (RectTransform)replaySave.transform; saveRect.anchoredPosition = new Vector2(264, -80); saveRect.sizeDelta = new Vector2(124, 38);
                replaySave.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta = saveRect.sizeDelta;
                replayLoad = ReplayButton("Replay Load", "저장하기", 0, OpenReplaySavePanel);
                var loadRect = (RectTransform)replayLoad.transform; loadRect.SetParent(undoButton.transform.parent, false);
                loadRect.anchorMin = loadRect.anchorMax = new Vector2(.5f, 0); loadRect.pivot = new Vector2(.5f, 0);
                loadRect.anchoredPosition = new Vector2(110, 18); loadRect.sizeDelta = new Vector2(200, 52);
                undoButtonRect.anchoredPosition = new Vector2(-110, 18);
                RefreshReplaySlots();
            }
            UpdateReplayTitle();
            UpdateReplayFolders();
            replayLoad.gameObject.SetActive(IsLocalPractice && !IsReplayBrowser && !matchArchiveSaveMode); replayLoad.interactable = ReplaySlotReady;
            replaySave.gameObject.SetActive(!replayLoadMode);
            replaySave.interactable = (matchArchiveSaveMode ? OnlineAdapter != null && OnlineAdapter.CanSaveMatchReplay : CanReplayShot) && selectedReplaySlot >= 0;
            for (int i = 0; i < ReplaySlotCount; i++)
            {
                replaySlotButtons[i].interactable = ReplaySlotReady && (!replayLoadMode || replaySlotFilled[i]);
                replaySlotButtons[i].GetComponent<Image>().color = i == selectedReplaySlot ? new Color(.72f, .46f, .08f) :
                    replaySlotFilled[i] ? new Color(.1f, .48f, .4f) : new Color(.24f, .27f, .3f);
            }
        }
    }
}
