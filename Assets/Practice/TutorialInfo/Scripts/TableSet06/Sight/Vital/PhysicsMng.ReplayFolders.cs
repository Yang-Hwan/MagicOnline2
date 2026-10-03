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
        public sealed class ReplayFolder
        {
            public string Id { get; internal set; }
            public string Name { get; internal set; }
        }
        string ReplayRootDirectory => Path.Combine(Application.persistentDataPath,
            (Assets.Scripts.Often.PracticeSceneFlow.SelectedMode == Assets.Scripts.Often.PracticeSceneFlow.Mode.MatchReplay || matchArchiveSaveMode) ? "MatchReplays" : "PracticeReplays");
        public string MatchReplaySlotDirectory => Path.Combine(Application.persistentDataPath, "MatchReplays");
        string selectedReplayFolder = "";
        public string SelectedReplayFolder => selectedReplayFolder;
        readonly List<Button> replayFolderButtons = new List<Button>();
        RectTransform replayFolderContent;
        ScrollRect replayFolderScroll;
        Button replayFolderAdd, replayFolderDelete;
        GameObject replayFolderDialog;
        TMP_InputField replayFolderName;
        TMP_Text replayFolderPrompt;
        Button replayFolderConfirm;
        bool replayFolderDeleting;
        string replayFolderDeleteId;

        public List<ReplayFolder> GetReplayFolders()
        {
            var folders = new List<ReplayFolder> { new ReplayFolder { Id = "", Name = "기본" } };
            string root = Path.Combine(ReplayRootDirectory, "Folders");
            try
            {
                if (Directory.Exists(root))
                    foreach (string path in Directory.GetDirectories(root))
                    {
                        string id = Path.GetFileName(path);
                        if (!Guid.TryParseExact(id, "N", out _) ||
                            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
                        string nameFile = Path.Combine(path, "name.txt");
                        if (File.Exists(nameFile)) folders.Add(new ReplayFolder { Id = id,
                            Name = NormalizeReplayTitle(File.ReadAllText(nameFile)) });
                    }
                folders.Sort(1, folders.Count - 1, Comparer<ReplayFolder>.Create((a, b) =>
                { int order = string.CompareOrdinal(a.Name, b.Name); return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id); }));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { replaySlotMessage = "폴더를 읽을 수 없습니다 · 저장 권한을 확인하세요"; }
            return folders;
        }

        public bool SelectReplayFolder(string id)
        {
            if (!ReplaySlotReady || !GetReplayFolders().Exists(folder => folder.Id == id)) return false;
            if (selectedReplayFolder == id) return true;
            // Finish the old title before changing the directory it belongs to.
            CommitReplayTitle();
            StopReplayView();
            selectedReplayFolder = id;
            selectedReplaySlot = -1; replayTitleSlot = -1;
            if (replayTitle) replayTitle.SetTextWithoutNotify("");
            replayTitleSaved = "";
            if (replayLoadMode)
            {
                replayFrames.Clear(); replayStroke = default; ReplayFrameIndex = 0;
            }
            RefreshReplaySlots();
            replaySlotMessage = replayLoadMode ? "불러올 번호를 선택하세요" : "저장할 번호와 제목을 선택하세요";
            RefreshReplayFolderButtons();
            return true;
        }

        public bool CreateReplayFolder(string name)
        {
            if (!ReplaySlotReady) return false;
            name = NormalizeReplayTitle(name);
            if (string.IsNullOrWhiteSpace(name) || GetReplayFolders().Exists(folder =>
                string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase)))
            { replaySlotMessage = "비어 있거나 같은 이름의 폴더가 있습니다"; return false; }
            string id = Guid.NewGuid().ToString("N");
            string path = Path.Combine(ReplayRootDirectory, "Folders", id);
            try
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "name.txt"), name, System.Text.Encoding.UTF8);
                return SelectReplayFolder(id);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { replaySlotMessage = "폴더 생성 실패 · 저장 공간과 권한을 확인하세요"; return false; }
        }

        // Called only after the in-game delete confirmation. The default directory cannot be removed.
        public bool DeleteReplayFolder(string id)
        {
            if (!ReplaySlotReady || string.IsNullOrEmpty(id) || !Guid.TryParseExact(id, "N", out _) ||
                !GetReplayFolders().Exists(folder => folder.Id == id)) return false;
            string path = Path.Combine(ReplayRootDirectory, "Folders", id);
            try
            {
                if (selectedReplayFolder == id && !SelectReplayFolder("")) return false;
                Directory.Delete(path, true);
                RefreshReplayFolderButtons();
                replaySlotMessage = "폴더와 저장 기록을 삭제했습니다";
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { replaySlotMessage = "폴더 삭제 실패 · 파일 사용 여부와 권한을 확인하세요"; return false; }
        }

        void SizeReplayControl(Button button, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)button.transform;
            rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = button.GetComponentInChildren<TMP_Text>();
            text.rectTransform.sizeDelta = size; text.fontSize = 18;
            text.richText = false; text.overflowMode = TextOverflowModes.Ellipsis;
        }

        void UpdateReplayFolders()
        {
            if (!replayFolderContent)
            {
                var viewport = new GameObject("Replay Folders", typeof(RectTransform), typeof(Image),
                    typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
                viewport.SetParent(replayPanel, false);
                viewport.anchoredPosition = new Vector2(-75, IsMatchReplayLayout ? 140 : 90); viewport.sizeDelta = new Vector2(510, 32);
                viewport.GetComponent<Image>().color = new Color(.06f, .13f, .16f);
                replayFolderContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
                replayFolderContent.SetParent(viewport, false);
                replayFolderContent.anchorMin = replayFolderContent.anchorMax = new Vector2(0, .5f);
                replayFolderContent.pivot = new Vector2(0, .5f);
                replayFolderScroll = viewport.GetComponent<ScrollRect>();
                replayFolderScroll.viewport = viewport; replayFolderScroll.content = replayFolderContent;
                replayFolderScroll.horizontal = true; replayFolderScroll.vertical = false;
                replayFolderScroll.movementType = ScrollRect.MovementType.Clamped;
                replayFolderScroll.scrollSensitivity = 30;
                replayFolderAdd = ReplayButton("Replay Folder Add", "+ 추가", 0, () => ShowReplayFolderDialog(false));
                replayFolderDelete = ReplayButton("Replay Folder Delete", "삭제", 0, () => ShowReplayFolderDialog(true));
                float folderY = IsMatchReplayLayout ? 140 : 90;
                SizeReplayControl(replayFolderAdd, replayPanel, new Vector2(226, folderY), new Vector2(76, 32));
                SizeReplayControl(replayFolderDelete, replayPanel, new Vector2(305, folderY), new Vector2(70, 32));
                RefreshReplayFolderButtons();
            }
            replayFolderAdd.interactable = ReplaySlotReady;
            replayFolderDelete.interactable = ReplaySlotReady && selectedReplayFolder != "";
            foreach (var button in replayFolderButtons) button.interactable = ReplaySlotReady;
        }

        void RefreshReplayFolderButtons()
        {
            if (!replayFolderContent) return;
            foreach (var button in replayFolderButtons) { button.gameObject.SetActive(false); Destroy(button.gameObject); }
            replayFolderButtons.Clear();
            float x = 0, selectedX = 0;
            foreach (var folder in GetReplayFolders())
            {
                string id = folder.Id;
                var button = ReplayButton("Replay Folder " + id, folder.Name, 0, () => SelectReplayFolder(id));
                SizeReplayControl(button, replayFolderContent, new Vector2(x + 68, 0), new Vector2(132, 32));
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
                if (id == selectedReplayFolder)
                { button.GetComponent<Image>().color = new Color(.1f, .48f, .4f); selectedX = x; }
                replayFolderButtons.Add(button); x += 138;
            }
            replayFolderContent.sizeDelta = new Vector2(Mathf.Max(510, x), 32);
            replayFolderScroll.StopMovement();
            replayFolderContent.anchoredPosition = new Vector2(-Mathf.Clamp(selectedX - 180, 0, Mathf.Max(0, x - 510)), 0);
        }

        void ShowReplayFolderDialog(bool deleting)
        {
            if (!ReplaySlotReady || (deleting && selectedReplayFolder == "")) return;
            CommitReplayTitle();
            if (!replayFolderDialog)
            {
                replayFolderDialog = new GameObject("Replay Folder Dialog", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)replayFolderDialog.transform; rect.SetParent(replayPanel, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                replayFolderDialog.GetComponent<Image>().color = new Color(.025f, .07f, .085f, .99f);
                replayFolderPrompt = CreateReplayTitleText(rect, "Prompt");
                replayFolderPrompt.rectTransform.anchorMin = replayFolderPrompt.rectTransform.anchorMax = new Vector2(.5f, .5f);
                replayFolderPrompt.rectTransform.sizeDelta = new Vector2(610, 68);
                replayFolderPrompt.rectTransform.anchoredPosition = new Vector2(0, 65);
                replayFolderPrompt.alignment = TextAlignmentOptions.Center;
                // Clone the existing title field's visual structure without its title-saving listener.
                replayFolderName = Instantiate(replayTitle, rect);
                replayFolderName.name = "Replay Folder Name";
                replayFolderName.onEndEdit = new TMP_InputField.SubmitEvent();
                replayFolderName.onValueChanged = new TMP_InputField.OnChangeEvent();
                ((RectTransform)replayFolderName.transform).anchoredPosition = new Vector2(0, 0);
                ((TMP_Text)replayFolderName.placeholder).text = "폴더 이름 (최대 40자)";
                replayFolderConfirm = ReplayButton("Replay Folder Confirm", "추가", 0, ConfirmReplayFolderDialog);
                var cancel = ReplayButton("Replay Folder Cancel", "취소", 0, () => replayFolderDialog.SetActive(false));
                SizeReplayControl(replayFolderConfirm, rect, new Vector2(-76, -66), new Vector2(140, 36));
                SizeReplayControl(cancel, rect, new Vector2(76, -66), new Vector2(140, 36));
            }
            replayFolderDeleting = deleting; replayFolderDeleteId = selectedReplayFolder;
            replayFolderDialog.SetActive(true); replayFolderDialog.transform.SetAsLastSibling();
            replayFolderName.gameObject.SetActive(!deleting); replayFolderName.interactable = true;
            replayFolderName.SetTextWithoutNotify("");
            var folder = GetReplayFolders().Find(item => item.Id == selectedReplayFolder);
            replayFolderPrompt.text = deleting ? $"‘{folder?.Name}’ 폴더를 삭제할까요?\n폴더 안의 기록과 제목도 모두 삭제됩니다." : "새 폴더 이름을 입력하세요";
            replayFolderConfirm.GetComponentInChildren<TMP_Text>().text = deleting ? "삭제" : "추가";
            if (!deleting) replayFolderName.ActivateInputField();
        }

        void ConfirmReplayFolderDialog()
        {
            bool success = replayFolderDeleting ? DeleteReplayFolder(replayFolderDeleteId) : CreateReplayFolder(replayFolderName.text);
            if (success) replayFolderDialog.SetActive(false);
            else replayFolderPrompt.text = replaySlotMessage;
        }
    }
}
