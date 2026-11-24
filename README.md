# MX Backlight Control

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6.svg)](#gereksinimler)

Logitech MX serisi klavyelerin (MX Keys, MX Keys S, MX Keys Mini, MX Mechanical vb.) arka ışığını **Sürekli Açık**, **Sürekli Kapalı** veya **Otomatik (Varsayılan)** modlarında kontrol etmek için sıfırdan geliştirilmiş yerel Windows (C# / .NET 9 WPF + Windows HID API) uygulaması.

## Özellikler

- **Sıfırdan Yerel HID++ 2.0 / 4.5 Motoru:** Harici sürücü veya 3. parti HID kütüphanesi kullanmadan doğrudan Windows `setupapi.dll`, `hid.dll` ve `kernel32.dll` P/Invoke katmanı ile klavyeyle haberleşir.
- **3 Kontrol Modu:**
  - **Sürekli Açık (Always On):** Güç tasarrufu / ortam sensörü kesintisini devre dışı bırakır ve yapılandırılabilir canlı tutma (keep-alive) döngüsüyle klavye ışığının sönmesini engeller.
  - **Sürekli Kapalı (Always Off):** Donanım seviyesinde arka ışığı kapatır; el yakınlık sensörü veya tuş basımı ışığı tekrar açmaz. Uyku/yeniden bağlanma sonrası da kapalı tutar.
  - **Otomatik (Varsayılan):** Logitech'in standart yakınlık ve ortam ışığı sensörü davranışını geri yükler.
- **Geniş Bağlantı Desteği:** Bluetooth LE (`0xFF43`), Logi Bolt (`0xC548`), Unifying (`0xC52B`) ve USB bağlantıları destekler.
- **Canlı Donanım Bilgisi:** Klavye adı, bağlantı türü, pil yüzdesi, aktif parlaklık seviyesi ve HID++ protokol sürümü.
- **Sistem Tepsisi (Tray) & Global Kısayol:** Arka planda sessizce çalışır; sağ tık menüsünden veya `Ctrl + Alt + L` kısayoluyla anında mod değiştirilebilir.
- **Windows ile Otomatik Başlatma:** İsteğe bağlı olarak Windows açılışında tepside başlar.

## Çalıştırma & Derleme

```powershell
dotnet build MxBacklightControl.sln -c Release
```

Derlenen uygulama yolu:
`MxBacklightControl\bin\Release\net9.0-windows\MxBacklightControl.exe`

Doğrudan çalıştırmak için:
```powershell
dotnet run --project MxBacklightControl\MxBacklightControl.csproj -c Release
```

## Gereksinimler

- Windows 10 / 11
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (kaynak koddan derlemek için)

## Katkıda Bulunma

Katkılarınız memnuniyetle karşılanır! Ayrıntılar için [CONTRIBUTING.md](CONTRIBUTING.md) dosyasına göz atabilirsiniz.

## Lisans

Bu proje [MIT Lisansı](LICENSE) altında açık kaynak olarak lisanslanmıştır.

