

# Theo dõi công việc

Cập nhật gần nhất: 2026-10-08 (Asia/Bangkok).

## Cách tiếp tục sau khi phiên làm việc bị ngắt

- Đọc `AGENTS.md` và file này trước khi sửa dự án.
- Kiểm tra `git status --short`: dự án đang có nhiều thay đổi chưa commit từ các phiên trước. Không reset, ghi đè hoặc xóa các thay đổi đó.
- Cập nhật file này ngay khi nhận task, hoàn thành một mốc, phát hiện lỗi hoặc bị chặn. Không chờ đến cuối phiên.
- Chỉ đánh dấu hoàn thành khi có bằng chứng kiểm tra; ghi rõ phần chưa chạy được.
- Khi task hoàn tất, giữ lại mục lịch sử và kết quả thay vì xóa.

## Dọn dẹp file rác — 2026-10-08

### Đã xóa

- **Logs/**: `TerrainRemovalValidation.log`, `TerrainRemovalValidation.txt`, `shadercompiler-UnityShaderCompiler.exe-0.log`, toàn bộ `Logs/AIReviewProject/`
- **ArtSource/**: `TerrainRemoval_20261008.json`, `cleanup_terrain_20261008.py`, `inspect_mother.py`, `validate_mother.py`, `rig_mother.py`, `mother_back.png`, `mother_front.png`, `mother_side.png`
- **Tests/**: Toàn bộ thư mục (checklist .md, script .ps1, file test .cs)
- **ArtSource/ForestBackups/**: Toàn bộ (backup zip tree polish + scene backup)
- **ArtSource/RetiredAssets/**: Toàn bộ (archive terrain + vegetation cũ)

- **Assets/_Project/Editor/**: `TerrainRemovalValidation.cs`, `ForestTerrainSetup.cs`, `ForestVegetationCleanup.cs` (+ .meta) — script terrain đã xóa, không còn dùng

### Còn lại trong ArtSource/

- `BlackpineRangerCabin/`, `MotherMoster/`, `TraditionalDoubleBarrel/` — source model đang dùng
- `MODEL_LIST.md` — danh sách model

---

## Yêu cầu người dùng trong phiên 2026-09-26

1. **Tiếp tục hoàn thiện phần đang chỉnh sửa dở.** Đang thực hiện.
   - Người dùng chưa chỉ định tính năng cụ thể. Dựa trên thời gian sửa file và log, phần gần nhất là menu chính tiếng Việt và luồng chuyển vào ForestDemo.
   - Phạm vi tiếp tục hiện tại: sửa lỗi kiểm tra menu, xác nhận vào màn rừng và kiểm tra hồi quy cốt truyện/súng.
2. **Tạo file lưu các task được giao để có thể tiếp tục khi hết limit bất ngờ.** Đã tạo file này.
   - Đây là nhật ký tiến độ dùng cho các phiên tiếp theo.

## Task đang làm: hoàn thiện menu → ForestDemo

### Trạng thái trước khi tiếp tục

- Có các scene `MainMenu`, `ForestDemo`, `SampleScene`; Build Settings đã đặt MainMenu đầu tiên.
- Menu có bắt đầu mới, tiếp tục checkpoint, điều khiển, cài đặt âm lượng/toàn màn hình và thoát.
- Cốt truyện Blackpine 3A–3F đã có mã prototype. Chưa có bằng chứng hoàn thành chơi thử toàn tuyến.
- `Logs/MainMenuValidation.txt` từ 2026-09-25 báo `Sequence contains no matching element` khi validator tìm nút “VÀO RỪNG”. Chưa xác định nguyên nhân gốc.
- `Logs/MainMenuValidation.log` cũ báo lỗi biên dịch `CanvasRenderer.GetMesh` không có overload nhận một tham số.

### Đã làm trong phiên này

- Đọc hướng dẫn, diff trạng thái, checklist và mã menu/validator.
- Sửa `Assets/_Project/Editor/ForestMenuValidation.cs`:
  - Bỏ đoạn debug gọi `GetMesh(mesh)` không tương thích, gây lỗi biên dịch.
  - Khi tìm nút thất bại, báo tên nút cần tìm và các nút đang hiển thị.
  - Kiểm tra nút có tương tác được trước khi gọi onClick.
- `Tests/Compile-Project.ps1`: thành công (exit 0), cả runtime và editor; có thông báo thông tin USG0001 về AdditionalFile.
- `Tests/Run-ForestStoryTests.ps1`: PASS — tiến trình bắt buộc, kết thúc không giết/có giết, nhánh tùy chọn, tương tác lặp và đạn trong checkpoint.
- `Tests/Run-ShotgunTests.ps1`: PASS — 6 tình huống đạn/tốc độ bắn/nạp đạn.

### Đang bị chặn / chưa xác minh

- Lần chạy Unity batch trong sandbox ghi `attempt to write a readonly database`; log tiếp theo báo không kết nối được Unity Package Manager IPC sau 30 giây và thoát mã 1.
- Log của lần chạy mới: `Logs/MainMenuResume.log`. Đây là lỗi môi trường chạy, chưa phải kết quả kiểm tra menu.
- Đã chạy lại Unity ngoài sandbox theo quy trình escalation: **PASS** kiểm tra menu, các panel, trạng thái checkpoint, biên nút và chuyển vào ForestDemo ở trạng thái Ready. Lỗi tìm nút cũ không tái hiện trong lần này.
- Đã xem ảnh `Logs/MainMenu.png`: chữ và nút hiển thị, nhưng không thấy rõ nền cây được tạo bởi ForestMenuBackdrop. Cần kiểm tra riêng nền này trước khi coi phần hình ảnh hoàn chỉnh.
- Phát hiện validator placement đòi tối thiểu 14 item, trong khi bootstrap tạo 13 item. Đã sửa `ForestStoryPlayValidation.cs` để kiểm tra mỗi ID cần thiết xuất hiện đúng một lần, cùng 3 creature hợp lệ. Đang chuẩn bị chạy validator này.
- Chưa thay đổi scene, terrain hoặc các pack cây trong phiên này.

### Bước tiếp theo theo thứ tự

1. Chạy lại Unity validator với quyền thích hợp, sau khi bảo đảm tiến trình chạy trước đã kết thúc:
   ```powershell
   & 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe' -batchmode -projectPath C:\Users\namphung\FPS -executeMethod ForestMenuValidation.Run -logFile Logs/MainMenuResume.log
   ```
   Không thêm `-quit`: validator tự thoát khi hoàn tất. Nếu gặp lỗi license thì ghi rõ và không báo Play Mode đã đạt.
2. Đọc báo cáo mới `Logs/MainMenuValidation.txt` và log Unity. Nếu nút vẫn mất, dùng danh sách nút mới bổ sung để tìm nguyên nhân, kiểm tra EventSystem/input và thứ tự frame của validator.
3. Khi vào ForestDemo, xử lý lỗi dựng story nếu có. Sau đó chạy `ForestStoryPlayValidation.RunBatch` bằng Unity để kiểm tra placement/navigation.
4. Xem ảnh menu được tạo dưới `Logs/MainMenu*.png`; kiểm tra bố cục. Chạy lại compile/tests khi thay đổi mã có liên quan.
5. Đã cập nhật `Tests/BlackpineDemoChecklist.md` để phản ánh MainMenu đứng đầu Build Settings và cần kiểm tra chuyển scene từ menu.
6. Ghi rõ kết quả tự động và phần chơi thử thủ công còn lại trong file này và trả lời người dùng.

## Các hạng mục tồn từ trước (suy ra từ tài liệu, không phải yêu cầu mới)

- `Tests/BlackpineDemoChecklist.md`: prototype, kiểm tra góc nhìn thứ nhất toàn tuyến, checkpoint, combat, ending và build standalone còn cần xác nhận.
- `Tests/ForestChecklist.md`: terrain sau chỉnh sửa từng lỗi navigation `Missing route endpoints`; các lần PASS cũ áp dụng cho terrain trước đó.
- Tree1/Tree2 là hai pack khác nhau, giữ lại. Khi thay pack, phải di chuyển tham chiếu ở scene/prefab/TerrainData và backup trước khi dọn asset cũ theo `AGENTS.md`.
- Backup/recovery nằm tại `ArtSource/ForestBackups` và `ArtSource/RetiredAssets`; không tự xóa.

## File liên quan chính

- `Assets/_Project/Scripts/Core/ForestMainMenu.cs`
- `Assets/_Project/Scripts/Core/ForestMenuFlow.cs`
- `Assets/_Project/Scripts/Core/ForestMenuBackdrop.cs`
- `Assets/_Project/Editor/ForestMenuValidation.cs`
- `Assets/_Project/Scripts/Core/ForestStoryBootstrap.cs`
- `Assets/_Project/Scripts/Core/ForestStoryDirector.cs`
- `Assets/_Project/Editor/ForestStoryPlayValidation.cs`
- `Tests/BlackpineDemoChecklist.md`

Lưu ý: `Logs/` bị git bỏ qua, có thể không tồn tại trên máy khác. Các kết quả quan trọng đã được tóm tắt ở trên.

## Task mới 2026-10-08: xóa map terrain và dọn file rác
- Đang thực hiện. Yêu cầu này thay thế hướng hoàn thiện map ForestDemo; các kết quả và hạng mục cũ bên trên giữ làm lịch sử, chưa coi đã hoàn thành.
- Đã kiểm tra: có nhiều thay đổi chưa commit, 3 scene và 3 phiên bản TerrainData. Giữ asset nguồn Tree1/Tree2 và backup cũ.
- Bước tiếp theo: xác định scene/phụ thuộc, lưu recovery ngoài Assets, gỡ map và chỉ dọn tài nguyên sinh ra không còn được dùng; kiểm tra tham chiếu và compile.

- Đã chuẩn bị script preview ArtSource/cleanup_terrain_20261008.py: gỡ ForestDemo, gỡ Terrain trong SampleScene, cập nhật Build Settings/menu, kiểm tra GUID trước khi xóa và tạo ZIP phục hồi có đối chiếu từng byte.
- Chưa áp dụng: Unity PID 16980 đang mở dự án FPS. Đã nhờ người dùng lưu và đóng Unity để tránh ghi đè scene. Không xóa Temp/Library khi Unity đang sử dụng.
- Bước tiếp theo: khi người dùng xác nhận đóng Unity, chạy script --apply, compile, kiểm tra tham chiếu sau xóa và dọn log cũ.

- Người dùng xác nhận đã đóng Unity. Đã kiểm tra PID 16980 không còn; bắt đầu áp dụng cleanup và lưu ZIP phục hồi.
- Đã áp dụng: xóa ForestDemo, terrain cũ trong SampleScene và 7.306.220 byte asset/map sinh ra. ZIP phục hồi đã kiểm tra CRC và đối chiếu nội dung trước khi xóa.
- Compile runtime/editor thành công; đã sửa cảnh báo unreachable code trong phần khóa checkpoint cũ. Bước tiếp theo: mở scene bằng Unity batch để kiểm tra missing script/reference, sau đó dọn log/tệp tạm.

- Dọn Logs: đã xóa 81 file output/log cũ trước 2026-10-08, tổng 10612916 byte. Giữ log kiểm tra mới và cache Library để tránh import lại toàn dự án.

### Hoàn tất cleanup 2026-10-08
- Đã xóa ForestDemo khỏi Assets/Build Settings, gỡ Terrain trong SampleScene; nút Chơi mới trỏ SampleScene. Giữ player, sàn và đối tượng thử nghiệm có sẵn.
- Không còn TerrainData trong Assets. Đã gỡ 112 file asset/scene/meta (7.306.220 byte), xóa thư mục ForestDemo/ForestRefresh rỗng và meta đi kèm. Tree1/Tree2, code gameplay/editor, backup cũ và save cũ được giữ.
- Dọn 81 file log/output cũ (10.612.916 byte), cùng output DLL/PDB/RSP của lần compile. Không xóa cache Library.
- Recovery ZIP: ArtSource/RetiredAssets/TerrainRemoval_20261008.zip (2.665.778 byte); đã kiểm tra CRC và từng byte trước khi xóa. Các backup lịch sử ngoài Assets không bị sửa; để phục hồi chúng cần khôi phục asset từ ZIP này.
- Kiểm chứng: compile runtime/editor PASS. Unity batch ngoài sandbox PASS: MainMenu/SampleScene mở được, không missing script/reference, không Terrain/TerrainData, player/Ground còn, menu trỏ SampleScene, checkpoint map cũ bị khóa. Báo cáo Logs/TerrainRemovalValidation.txt. Lần sandbox trước đó lỗi readonly database/UPM IPC; đã chạy lại thành công.
- Chưa kiểm tra Play Mode hoặc build standalone; không suy ra gameplay đã PASS. Các validator dựng/chơi map rừng cũ không còn áp dụng; giữ nguồn để tái sử dụng.
- Yêu cầu xóa map/dọn file rác đã hoàn tất. Bước tiếp theo: người dùng xác định hướng game mới; dùng SampleScene làm scene thử nghiệm hoặc tạo scene mới. Không tiếp tục tự hoàn thiện ForestDemo.

