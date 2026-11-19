<img width="292" height="389" alt="image" src="https://github.com/user-attachments/assets/d33fdfba-7877-4cbb-8fe9-ad2cc820ce0e" />

## เชื่อมต่อฐานข้อมูลบนเครื่องอื่น

ติดตั้ง .NET SDK 10 แล้วเปิด PowerShell ในโฟลเดอร์โปรเจกต์ ตั้งค่า connection string จาก Filess ใน user secrets ของเครื่องนั้น (ไม่ต้องใส่รหัสผ่านในไฟล์โปรเจกต์หรือ Git):

```powershell
$server = (Read-Host "Host จาก Filess").Replace("'", "''")
$port = Read-Host "Port จาก Filess"
$database = (Read-Host "Database name จาก Filess").Replace("'", "''")
$user = (Read-Host "User จาก Filess").Replace("'", "''")
$secret = Read-Host "Password จาก Filess" -AsSecureString
$password = ([System.Net.NetworkCredential]::new('', $secret).Password).Replace("'", "''")
$connection = "Server='$server';Port=$port;Database='$database';User ID='$user';Password='$password';SslMode=Required"
@{ 'ConnectionStrings:DefaultConnection' = $connection } | ConvertTo-Json -Compress | dotnet user-secrets set --project .\Gamesgiftshop.csproj
Remove-Variable secret, password, connection
dotnet run --project .\Gamesgiftshop.csproj
```

User secrets ใช้เฉพาะเครื่องและผู้ใช้ Windows นั้น และโหลดโดยอัตโนมัติใน environment `Development` เท่านั้น หากใช้ environment อื่น ให้กำหนด environment variable `ConnectionStrings__DefaultConnection` อย่างปลอดภัยแทน

ฐานข้อมูล Filess ที่ตั้งค่าไว้สร้างตารางแล้วและเริ่มต้นว่าง ไม่ต้องรัน migration ซ้ำบนเครื่องอื่น หากใช้ฐานข้อมูลใหม่ที่ยังไม่มีตาราง ให้ตรวจสอบเป้าหมายก่อนรัน `dotnet ef database update --project .\Gamesgiftshop.csproj -- --environment Development` ห้ามใช้ initial migration นี้กับฐานข้อมูลเดิมที่มีตารางอยู่
