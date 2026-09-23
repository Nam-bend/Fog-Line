# Shoot: hiện trạng và kiểm thử

## Phân chia code

- `FirstPersonWeapon`: điều phối input, bắn, feedback, UI và animation của model. Giữ các serialized field để scene hiện tại tiếp tục dùng được.
- `ShotgunAmmo`: hai buồng đạn (Empty / Live / Spent), đạn dự trữ, bắn, lấy vỏ và nạp viên mới.
- `ShotgunAction`: cooldown, chặn bắn khi reload, tiến trình reload, hủy reload và các sự kiện theo mốc thời gian. Không phụ thuộc Unity.
- `ShotgunHitDetection`: hướng ngắm từ camera, kiểm tra vật cản trước nòng, spread, pellet và sát thương giảm theo khoảng cách. Cộng sát thương pellet trước khi gọi TakeDamage một lần cho mỗi enemy.
- `ShotgunEffects` / `ShotgunAudio`: hiệu ứng và âm thanh.
- `PlayerUI` / `PlayerLook`: UI và camera recoil.
- `EnemyHealth` / `EnemyAI`: nhận sát thương, hit reaction, ngắt đòn đánh và chết.

## Đối chiếu task

| Task | Đã có trong code | Còn thiếu / mở rộng |
| --- | --- | --- |
| Shoot Input | Chuột trái semi-auto; chặn khi reload, cooldown, unequip, chết, pause | Automatic; trạng thái đang đổi súng. Fire/Reload đang đọc trực tiếp Mouse/Keyboard, chưa có Input Actions để remap/gamepad |
| Ammo | Loaded tương ứng currentAmmo; Capacity = 2; Reserve tương ứng reserveAmmo; mỗi phát dùng một viên | Magazine dùng chung cho rifle nếu thêm loại súng mới; pickup nếu gameplay cần |
| Fire Rate | fireInterval mặc định 0.8 giây; dry fire có cooldown riêng | Không thiếu cho shotgun hiện tại |
| Hit Detection | Ngắm từ camera, sphere cast bán kính nhỏ từ nòng; nhiều pellet, spread; lọc owner và trigger; chặn nòng xuyên vật cản | Kiểm thử vật cản và enemy trong scene |
| Damage | Tổng damage mặc định 50/phát, chia đều cho pellet; giảm theo khoảng cách; TakeDamage | Head / Body / Limb multipliers |
| Weapon Feedback | Muzzle flash/light, âm thanh, recoil model/camera, impact, hit/kill marker | Screen shake riêng nếu cần; pooling nếu profiling cho thấy cấp phát gây giật |
| Shoot Animation | Idle, recoil rồi về idle, dry fire | Không cần pump cho shotgun hai nòng bẻ gập hiện tại |
| Reload | R; kiểm tra reserve/capacity; mở súng, văng vỏ, nạp từng viên, đóng súng; cập nhật UI; hủy không mất viên đã nạp | Animation tay nếu muốn; kiểm thử trực quan |
| Shoot UI | Crosshair trong SampleScene; Loaded / Reserve; hit marker; reload indicator | Không thiếu các mục cơ bản |
| Enemy Reaction | Trừ máu, hit animation, stagger/ngắt đòn đánh, death animation, tắt AI/collider; impact màu máu đơn giản | Knockback vật lý, ragdoll hoặc blood effect chi tiết nếu cần |

Automatic, đổi súng và magazine tổng quát là các task mở rộng; lần clean code này chưa thêm chúng.

## Kiểm tra tự động đã chạy

Chạy từ project root: `powershell -File Tests/Run-ShotgunTests.ps1`.
Nếu cài Unity ở vị trí khác, truyền `-UnityData <đường dẫn Editor/Data>`.

Sáu kịch bản chạy bằng compiler và Mono của Unity, không cần mở scene:

1. Bắn trái/phải, cooldown đúng biên, dry fire và không trừ reserve khi bắn.
2. Chặn reload khi đầy/hết reserve; chặn bắn khi reload; không restart khi nhấn R liên tục.
3. Reload một viên đã bắn, giữ nguyên viên còn sống.
4. Hủy sau khi nạp một viên rồi reload tiếp, không mất hoặc nhân đôi đạn.
5. Hủy sau khi văng vỏ rồi reload tiếp, không văng vỏ lần hai.
6. Frame dài đi qua mọi mốc reload, reserve chỉ còn một viên, sự kiện không phát lại.

Đã biên dịch Assembly-CSharp và Assembly-CSharp-Editor bằng Roslyn/reference của project, không có lỗi hoặc cảnh báo. Đây chưa phải xác nhận Play Mode hoặc player build.

## Test Play Mode theo thứ tự

Các bước dưới đây chưa được chạy trực tiếp trong lần refactor này. Hoàn thành từng bước trước khi chuyển sang bước tiếp theo.

1. **Input / Ammo / Fire rate:** bắt đầu 2 / 24; click ra 1 / 24 rồi 0 / 24; click nhanh trong 0.8 giây không mất thêm đạn; giữ chuột không bắn liên tục; hết đạn chỉ có dry fire.
2. **Reload:** R sau khi bắn một viên và sau khi bắn cả hai; kiểm tra lần lượt 2 / 23 và lượng reserve tương ứng; thử reserve = 1 và 0. Hủy bằng Unequip/disable ở các mốc trước văng vỏ, sau văng vỏ, sau nạp viên đầu; equip lại để kiểm tra số đạn.
3. **Hit / Damage:** bắn trượt, tường, enemy gần/xa, nhiều collider của cùng enemy và hai enemy trong vùng spread. Kiểm tra mỗi enemy nhận một hit reaction/phát và tường che được enemy; áp nòng sát tường không bắn xuyên.
4. **Feedback / UI:** kiểm tra flash, âm thanh, recoil, impact, shell insertion/ejection, hit marker, kill marker và reload indicator. Vỏ đang nạp không tự biến mất theo lifetime của vỏ văng.
5. **Enemy / Lifecycle:** bắn chết enemy, xác nhận AI và collider tắt; thử pause, chết và unequip giữa recoil/reload, không bắn tiếp hoặc để pose/UI bị kẹt.

Ưu tiên tiếp theo: hoàn tất Play Mode → chuyển Fire/Reload sang Input Actions nếu cần remap/gamepad → hệ thống đổi súng nếu có nhiều súng → automatic/magazine theo loại súng → hitbox và các hiệu ứng tùy chọn.
