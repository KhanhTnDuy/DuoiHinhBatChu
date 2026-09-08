# Thư mục nguyên liệu ảnh

Thả ảnh từng **vật** vào đây. Script `gen.js` sẽ tự dùng ảnh trong thư mục này
thay cho icon emoji mặc định.

## Quy tắc đặt tên

Tên file = tên vật, **viết thường không dấu**, đúng như script báo ra.

```
con cho.png
con ca.png
ngoi sao.png
```

Không phân biệt hoa thường và không phân biệt dấu, nên `Con Chó.png` cũng nhận.
Chấp nhận `.png`, `.jpg`, `.jpeg`, `.webp`.

## Chọn ảnh thế nào cho đẹp

1. **Ưu tiên PNG nền trong suốt.** Search kèm chữ `png` hoặc `transparent`
   (ví dụ: `dog png transparent`). Ảnh nền trắng cũng dùng được vì thẻ nền trắng,
   nhưng ảnh nền màu sẽ hiện thành một khối vuông xấu.
2. **Ảnh vuông hoặc gần vuông là đẹp nhất.** Script tự co cho vừa và **giữ nguyên
   tỉ lệ** (không kéo méo), nhưng ảnh quá dài sẽ trông nhỏ trong thẻ.
3. **Kích thước tối thiểu ~400×400.** Nhỏ hơn sẽ vỡ khi phóng lên.
4. **Phong cách nên đồng bộ cả bộ** — chọn hết clipart phẳng, hoặc hết ảnh thật.
   Trộn lẫn sẽ trông chắp vá.
5. **Vật không được chính là đáp án.** Ví dụ câu CÁ HEO thì ảnh "con cá" phải là
   con cá thường, không được lấy ảnh con cá heo.

## Nguồn ảnh gợi ý

- flaticon.com — icon phẳng, đồng bộ, có sẵn PNG nền trong
- freepik.com — nhiều clipart
- pngtree.com / cleanpng.com — PNG tách nền
- Google Hình ảnh → Công cụ → Loại → **Có nền trong suốt**

## Cách chạy lại

```
cd tools
npm install          # chỉ lần đầu
node gen.js "../Assets/CauHoi"
```

Chạy xong script sẽ liệt kê những vật **vẫn đang dùng icon dự phòng** —
đó chính là danh sách ảnh bạn còn cần tìm.
