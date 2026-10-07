# Model còn thiếu để người dùng tạo — Blackpine

Đối chiếu ngày 2026-09-27 với file model, mesh .asset, prefab trong Assets,
nguồn ArtSource và mã dựng vật thể runtime. Phạm vi: màn Blackpine hiện tại,
không phải tất cả nhân vật/địa điểm trong kịch bản toàn game.
Đây là danh sách giao việc tạo asset; chưa nhập hoặc thay model trong phiên này.

## Cập nhật 2026-09-28

Đã dựng lán low-poly theo yêu cầu mới trong Blender: nguồn, FBX và ảnh render ở
`ArtSource/BlackpineRangerCabin/`. FBX có 9 mesh, 5.994 tam giác; kiểm tra import lại
trong Blender đạt. Lán đã có model nguồn mới, **chưa thay prefab trong Unity**.
Hàng lán trong bảng bên dưới ghi lại hiện trạng trước khi dựng; không cần tạo lại.

## Danh sách tạo ban đầu

| Ưu tiên | Model | Hiện trạng đã kiểm tra | Yêu cầu tạo tối thiểu |
|---|---|---|---|
| 1 | Cụm máy công vụ có cầu dao | ForestStoryBootstrap dùng cube cho power, thêm Light làm đèn báo; chưa có model máy | Một máy cũ dùng ngoài rừng, kèm hộp điện và cần gạt. Tách cần gạt khỏi thân để làm chuyển động; đèn báo là phần riêng. Không cần thêm máy phát/bộ rung rời. |
| 1 | Cổng công vụ | serviceGate hiện là một cube | Một bộ khung/trụ và cánh cổng; cánh tách riêng, pivot ở bản lề nếu mở xoay. Không cần ổ khóa/chìa khóa riêng. |
| 1 | NPC Briggs | briggs hiện là cube; Player.prefab không phải model người cho NPC | Một người trưởng thành mặc đồ làm việc/đi rừng. Có rig; tối thiểu animation đứng nghỉ. Animation nói chuyện là phần bổ sung. |
| 1 | Lán kiểm lâm | Có RangerCabin.prefab nhưng tường, sàn và mái đều dùng mesh cube Unity; chưa có model lán hoàn thiện | Lán nhỏ có cửa vào và nội thất đi vào được, mái/tường/sàn; giữ khoảng trống để đặt sơ đồ. Không cần nhồi thêm đồ nhặt hoặc làm cả pack nội thất. |
| 2 | Tờ sơ đồ G-07 | diagram là cube; nội dung hiện hiển thị bằng UI chữ | Một tờ giấy cũ hơi gấp/cong, UV và texture sơ đồ. Có thể làm bằng plane đơn giản; không cần rig hay animation lật hai mặt. |
| 2 | Mảnh vải rách | cloth hiện là cube dẹt; chưa có mesh/texture riêng | Một miếng vải rách đơn giản, texture có alpha hoặc viền rách bằng mesh; không cần mô phỏng vải. |

## Chưa có model riêng nhưng có thể tái sử dụng hình hiện tại

- **Vỏ đạn shotgun đã bắn:** ShotgunEffects.CreateShellVisual tạo thân và đáy đạn
  bằng hai cylinder; dấu vết trên đường hiện vẫn là cube. Có thể nối hình này vào dấu vết,
  nên chưa bắt buộc bạn tạo thêm. Nếu muốn hoàn thiện mỹ thuật, tạo một vỏ đạn đã bắn
  với thân nhựa cũ, đáy đồng và miệng mở; không phải hộp đạn hay đạn nhặt được.

## Đã có — không cần tạo lại cho task này

| Asset | Bằng chứng trong dự án | Ghi chú |
|---|---|---|
| Thân cây/gỗ đổ | Assets/_Project/Environment/ForestDemo/log_large.prefab tham chiếu mesh log_large.asset | Có mesh riêng, không phải cube Unity. Có thể dùng cho đường chắn; bootstrap chưa gắn mesh này vào landslide. Chất lượng/hình dáng cụ thể cần xem khi đặt vào cảnh. |
| Đá | ForestDemo/rock_largeA.prefab + rock_largeA.asset; ForestRefresh/WeatheredBoulder.asset | Tái sử dụng cho cảnh quan/cover. |
| Cây | Environment/Tree1/source/trees1.fbx và Tree2/source/tree1.fbx | Giữ cả hai pack. |
| Quái nhỏ | Art/Enemies/MonsterPSX/MonsterPSX.fbx | Đã có model. |
| Mother | Art/Enemies/MotherMosterPSX/MotherMosterPSX.fbx | Đã có model; bóng lướt trong story vẫn đang là placeholder. |
| Shotgun hai nòng | Art/Weapons/TraditionalDoubleBarrel/Models/TraditionalDoubleBarrel.fbx | Có nguồn .blend/.glb trong ArtSource. |

## Khi đưa asset vào dự án

- Ưu tiên phong cách low-poly/PSX phù hợp cây, quái và súng đang có.
- Gửi FBX và texture PNG/TGA; giữ file Blender nguồn nếu có.
- Dùng tỉ lệ mét, đặt tên rõ cho các bộ phận chuyển động. Không gộp cánh cổng/cần gạt vào mesh tĩnh chung.
- Có thể đặt theo thư mục: Assets/_Project/Art/Props/<TenModel>, Art/Characters/Briggs,
  Art/Buildings/RangerCabin; đây là gợi ý tổ chức, chưa tạo thư mục trong phiên này.
- Sau khi bạn thêm model: kiểm tra import/material/tỉ lệ, gắn vào đúng placeholder,
  bảo toàn tương tác và checkpoint rồi kiểm tra va chạm/đường đi.

Không cần làm model cho túi đạn trang trí, ảnh, bếp manh mối, nhãn bảo trì,
mốc nhánh phụ/cầu tràn dạng item hoặc đồ sưu tập đã bỏ khỏi luồng chơi.
Radio và đèn pin chưa cần model cầm tay cho luồng hiện tại: đang dùng thoại/âm thanh và Light.
