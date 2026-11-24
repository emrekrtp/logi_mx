# Katkıda Bulunma (Contributing)

**MX Backlight Control** projesine katkıda bulunmak istediğiniz için teşekkürler!

## Nasıl Katkıda Bulunabilirsiniz?

1. Bu repoyu **fork** edin.
2. Yeni bir dal (branch) oluşturun:
   ```bash
   git checkout -b feat/yeni-ozellik
   ```
3. Değişikliklerinizi yapın ve projenin derlendiğinden emin olun:
   ```powershell
   dotnet build MxBacklightControl.sln -c Release
   ```
4. Değişikliklerinizi açıklayıcı bir mesajla commit edin:
   ```bash
   git commit -m "feat: yeni özellik açıklaması"
   ```
5. Dalınızı kendi fork'unuza push edin ve bir **Pull Request** açın.

## Hata Bildirimi & Özellik İstekleri

Bir hata bulursanız veya yeni bir klavye modeli / özellik eklemek isterseniz lütfen bir **Issue** açın. Mümkünse aşağıdaki bilgileri ekleyin:
- Klavye modeli (örn. MX Keys S, MX Mechanical Mini)
- Bağlantı türü (Bluetooth LE, Logi Bolt, Unifying, USB)
- Windows sürümü ve uygulama içi durum/hata çıktısı
