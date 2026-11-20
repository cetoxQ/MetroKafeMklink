# MetroKafe Mklink

İnternet kafe makinelerinde oyun ve launcher klasörlerini tek bir merkez klasörde toplayan, eski yerlerine
junction (`mklink /J`) koyan ve gerekli registry anahtarlarını taşıyan Windows uygulaması.

## Sekmeler

| Sekme | Ne yapar |
|---|---|
| **Mklink** | Bağlantıların listesi ve durumları (Bağlı / Bağlı değil / Gerçek klasör / Yanlış hedef / Hedef yok). Ekle, düzenle, sil, tek tıkla bağla, bağlantıyı kaldır. |
| **Registry** | Anahtarları merkezdeki `Registry` klasörüne `.reg` olarak dışa aktar, client'ta içe aktar, Regedit'te aç. |
| **Ayarlar** | Merkez klasör, işlem öncesi kapatılacak programlar, GitHub güncelleme kaynağı ve token. |
| **Güncelleme** | GitHub Releases'tan yeni sürümü denetler, indirir, kendini değiştirip yeniden başlar. |

## "Bağla" ne yapar?

Her kayıt için diskteki duruma bakar:

| Durum | Yapılan |
|---|---|
| Bağlantı yok, merkezde veri var | Junction oluşturulur |
| Gerçek klasör var, merkezde yok | Klasör merkeze **taşınır**, yerine junction konur |
| Gerçek klasör var, merkezde de var | Buradaki klasör `.yedek_tarih` adıyla kenara alınır, junction konur |
| Başka yeri gösteren junction | Silinip doğrusu oluşturulur |
| Hedef yok | Dokunulmaz, hata olarak gösterilir |

Uygulamadan önce yapılacaklar listelenir ve onay istenir. "Bağlantıyı kaldır" yalnızca junction'ı siler; veri merkezde kalır.

## BAT'a aktar

Mklink sekmesindeki **BAT'a aktar** düğmesi seçili kayıtları (seçim yoksa görünen tüm etkin kayıtları) tek bir
`.bat` dosyasına yazar. Dosya yönetici olarak çalıştırıldığında her kayıt için:

1. Hedef klasör yoksa kayıt atlanır ve hata olarak yazılır
2. Bağlantı yerinde mevcut bir mklink varsa `rmdir` ile kaldırılır
3. Gerçek klasör varsa adına `.eski` eklenir (`.eski` zaten varsa `.eski_tarih`) ve bu loglanır
4. `mklink /J` ile bağlanır

Bütün adımlar bat'ın yanındaki `mklink_log.txt` dosyasına yazılır. `{MERKEZ}` bat'ın başında `set "MERKEZ=..."`
satırına dönüşür; `%APPDATA%` gibi değişkenler bat'ı çalıştıran kullanıcıya göre çözülür.

## Yollar ve değişkenler

Merkez klasör varsayılan olarak boştur ve boşken **exe'nin bulunduğu klasör** (`.\`) kullanılır.
`.\Merkez` gibi göreli bir yol da exe'nin klasörüne göre çözülür; istenirse `D:\Merkez` gibi tam yol da yazılabilir.

Yollarda `{MERKEZ}` (Ayarlar'daki merkez klasör) ve `%APPDATA%`, `%LOCALAPPDATA%`, `%PROGRAMDATA%`, `%USERPROFILE%`
kullanılabilir. Böylece aynı kayıt her bilgisayarda ve her Windows kullanıcısında doğru yeri gösterir.
Junction yalnızca yerel bir diski gösterebilir; ağ yolu (`\\sunucu\...`) kullanılamaz.

## Komut satırı (client açılışı için)

```
MetroKafeMklink.exe /bagla [/tasi] [/kayitsiz]
```

- `/bagla`: etkin tüm bağlantıları kurar ve merkezdeki `.reg` dosyalarını içe aktarır (pencere açmaz)
- `/tasi`: gerçek klasörü merkeze taşımaya izin verir (varsayılan olarak taşımaz, atlar)
- `/kayitsiz`: registry adımını atlar

Sonuç `MetroKafeMklink.log` dosyasına yazılır. Çıkış kodu `0` sorunsuz, `1` hata var demektir.

## Dosyalar

- `MetroKafeMklink.json`: exe'nin yanında; bütün kayıtlar ve ayarlar burada. Exe ile birlikte client'lara kopyalanabilir.
- `MetroKafeMklink.log`: işlem kaydı.

## Güncelleme

Güncellemeler bu deponun Releases bölümünden alınır. Depo herkese açık olduğu için token gerekmez.
Gizli bir depo kullanılacaksa ya da GitHub istek sınırına takılınırsa Ayarlar'a *Contents: Read-only* izinli
fine-grained bir token girilebilir. Token bilgisayara özel (DPAPI) şifrelenerek saklanır.

Yeni sürüm yayınlamak:

```
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
gh release create v1.0.1 publish/MetroKafeMklink.exe --title "v1.0.1" --notes "..."
```

`MetroKafeMklink.csproj` içindeki `<Version>` etiketle aynı olmalıdır.

## Notlar

- Uygulama yönetici olarak çalışır (junction ve HKLM işlemleri için gerekli).
- Riot Vanguard (`C:\Program Files\Riot Vanguard`) junction ile taşınmamalı; her client'a normal kurulmalıdır.
- Gereksinim: Windows 10/11 x64. Yayınlanan exe .NET çalışma zamanını içinde taşır, ayrıca kurulum gerekmez.
