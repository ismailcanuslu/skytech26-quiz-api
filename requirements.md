# Proje: Kahoot Klonu Backend (REST API + WebSocket)
**Hedef:** Yaklaşık 300 eşzamanlı kullanıcıya anlık hizmet verebilecek, Clean Architecture prensiplerine uygun, yüksek performanslı bir .NET Backend sistemi geliştirmek.

## 0. Mimari ve Teknoloji Yığını (Tech Stack)
*   **Framework:** .NET 8 / .NET 10 Web API
*   **Mimari:** Clean Architecture (Domain, Application, Infrastructure, Presentation)
*   **Gerçek Zamanlı İletişim:** SignalR
*   **Veritabanı:** PostgreSQL (Entity Framework Core)
*   **Önbellekleme / In-Memory State:** Redis (Oyun state'i ve leaderboard yönetimi için kritik)
*   **Kimlik Doğrulama:** JWT (Admin için kalıcı, Oyuncular için geçici/anonim token)
*   **Konteynerizasyon:** Docker

---

## 1. Proje Kurulumu ve Altyapı
**1.1. Solution ve Katmanların Oluşturulması**
*   Domain, Application, Infrastructure ve API projelerinin oluşturulması.
*   Proje referanslarının (Dependency Rule'a uygun olarak) ayarlanması.
*   Global Exception Handling middleware'inin yazılması.

**1.2. Temel Paketlerin Kurulumu**
*   EF Core, Npgsql, MediatR, FluentValidation, SignalR, StackExchange.Redis kurulumları.

**1.3. Dependency Injection (DI) Yapılandırması**
*   Her katman için `ServiceCollectionExtensions` yazılması (örn: `AddApplicationServices`, `AddInfrastructureServices`).

---

## 2. Domain Modelleri ve Veritabanı (PostgreSQL)
**2.1. Admin ve Kullanıcı Entitileri**
*   `User` (Admin bilgileri).
*   *Not:* Oyuncular veritabanında kalıcı tutulmayacak, RAM/Redis üzerinde geçici olarak yaşayacaktır.

**2.2. Quiz ve Soru Entitileri**
*   `Quiz` (Id, Title, Description, CreatedAt, IsActive).
*   `Question` (Id, QuizId, Text, TimeLimit, Points).
*   `AnswerOption` (Id, QuestionId, Text, IsCorrect).

**2.3. EF Core Konfigürasyonu**
*   `DbContext`'in oluşturulması.
*   Entity konfigürasyonlarının (Fluent API) ayrılmış class'larda yazılması.
*   Initial Migration'ın alınması ve veritabanının güncellenmesi.

---

## 3. Kimlik Doğrulama ve Yetkilendirme (Auth)
**3.1. Admin Authentication**
*   Admin Login endpoint'i (Email/Password).
*   Başarılı girişte JWT dönülmesi.
*   Role-based authorization (Admin rolü) ayarlanması.

**3.2. Player (Guest) Authentication**
*   PIN ile oyuna giriş yapan oyuncular için geçici, claim'lerinde sadece `Nickname` ve `SessionId` barındıran kısa ömürlü bir JWT üretilmesi.

---

## 4. Admin API (Quiz Yönetimi)
**4.1. Quiz CRUD İşlemleri**
*   Yeni Quiz oluşturma endpoint'i.
*   Mevcut Quizleri listeleme ve detay getirme endpoint'leri.
*   Quiz silme/pasife alma işlemleri.

**4.2. Soru ve Cevap Yönetimi**
*   Bir Quiz'e soru ekleme, güncelleme ve silme endpoint'leri.
*   Her soru için doğru/yanlış cevap seçeneklerinin yapılandırılması.

---

## 5. Oyun Oturumu (Game Session) Yönetimi
**Not:** 300 eşzamanlı kullanıcının her cevap tıklamasında DB'ye gitmemesi gerekir. Oturum Redis üzerinde veya Thread-Safe bir Singleton MemoryCache üzerinde tutulmalıdır.

**5.1. Session Başlatma (Admin)**
*   Admin'in bir Quiz'i seçip "Başlat" (Host) demesiyle eşsiz bir 6 haneli `Game PIN` üretilmesi.
*   Game statüsünün `WaitingForPlayers` olarak Redis'e kaydedilmesi.

**5.2. Oyuncu Katılımı (Player)**
*   Oyuncunun `Game PIN` ve `Nickname` ile `Join` endpoint'ine istek atması.
*   PIN doğrulaması ve Nickname benzersizliği kontrolü (Aynı lobi içinde).
*   Başarılıysa geçici JWT verilmesi ve oyuncunun Redis'teki lobi listesine eklenmesi.

**5.3. Oyun Durum Makinesi (State Machine)**
*   Oyun durumlarının tanımlanması: `Waiting`, `QuestionActive`, `QuestionResult`, `Leaderboard`, `Finished`.

---

## 6. Gerçek Zamanlı İletişim (SignalR)
**6.1. Hub Kurulumu ve Grup Yönetimi**
*   `GameHub` sınıfının oluşturulması.
*   Bağlanan oyuncuların ve Admin'in, `Game PIN` adına sahip bir SignalR grubuna (`Groups.AddToGroupAsync`) dahil edilmesi.

**6.2. Admin -> Player Olayları (Events)**
*   `GameStarted`: Oyunun başladığını herkese bildirme.
*   `NextQuestion`: Yeni soruyu (sadece metin ve şıklar, doğru cevap gizli) ve süreyi yayınlama.
*   `ShowCorrectAnswer`: Süre bittiğinde doğru cevabı yayınlama.
*   `ShowLeaderboard`: Top 5 listesini yayınlama.
*   `EndGame`: Oyunun bittiğini bildirme ve bağlantıları koparma.

**6.3. Player -> Admin Olayları**
*   `PlayerJoined`: Yeni biri katıldığında Admin ekranında isminin belirmesi.
*   `SubmitAnswer`: Oyuncunun şıkkı seçmesi. (Sunucuya anında iletilir, SignalR veya hızlı bir REST endpoint üzerinden yapılabilir).

---

## 7. Skor Hesaplama ve Leaderboard (Core Logic)
**7.1. Puanlama Algoritması**
*   Oyuncunun cevabı doğruysa puan hesaplanması.
*   *Formül:* (Maks Puan) * (Kalan Süre / Toplam Süre) oranına göre dinamik puanlama.

**7.2. Leaderboard Güncellemesi**
*   Her soru sonrasında oyuncuların toplam puanlarının Redis üzerinde sıralı setler (Sorted Sets - `ZADD`) ile güncellenmesi.
*   Anlık sıralama verisinin çekilip Admin ve Oyuncu ekranlarına basılması.

---

## 8. Performans ve 300 Concurrent User Optimizasyonu
**8.1. Redis Optimizasyonu**
*   Oyun esnasında DB'ye (PostgreSQL) kesinlikle yazma yapılmaması. Tüm cevapların Redis'te toplanması.
*   Oyun bittiğinde (Finished statüsü), istenirse analitik amaçlı tüm sonuçların Bulk olarak DB'ye yazılması (Background Service / Hangfire / RabbitMQ ile).

**8.2. SignalR Optimizasyonu**
*   Hub metodlarının olabildiğince hafif tutulması.
*   (İleride yatay büyüme gerekirse) Redis Backplane entegrasyonu için yapılandırma bırakılması.

---

## 9. Test ve Deployment (Docker)
**9.1. Birim ve Entegrasyon Testleri**
*   Puanlama algoritmasının ve State Machine mantığının xUnit/NUnit ile test edilmesi.

**9.2. Dockerization**
*   `Dockerfile` oluşturulması (Multi-stage build).
*   Projenin API, PostgreSQL ve Redis servislerini tek seferde ayağa kaldıracak `docker-compose.yml` dosyasının hazırlanması.