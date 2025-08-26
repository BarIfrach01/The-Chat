# The Chat - Blazor Chat Application

## דרישות מערכת

### Windows:
- Windows 10/11 (64-bit)
- Docker Desktop for Windows
- WSL2 (Windows Subsystem for Linux)
- Git

### Mac/Linux:
- Docker
- Docker Compose
- Git

## התקנה והרצה

### שלב 1: הכנת הסביבה (Windows)

1. **התקן Docker Desktop**:
   - הורד מ: https://desktop.docker.com/win/stable/Docker%20Desktop%20Installer.exe
   - התקן והפעל את Docker Desktop
   - וודא שהוא רץ (סמל ירוק בשורת המשימות)

2. **עדכן WSL2** (חשוב מאוד!):
   
   **אופציה א - דרך PowerShell**:
   powershell
   # פתח PowerShell כמנהל מערכת
   wsl --update
   
   
   **אופציה ב - הורדה ידנית** (אם אופציה א לא עובדת):
   - לך ל: https://github.com/microsoft/WSL/releases/latest
   - הורד את הקובץ: `Microsoft.WSL_1.x.x.0_x64_ARM64.msixbundle`
   - התקן את הקובץ כמנהל מערכת

3. **הפעל מחדש WSL ו-Docker**:
powershell
   wsl --shutdown
   ואז סגור והפעל מחדש את Docker Desktop

### שלב 2: הורדת הפרויקט
git clone https://github.com/Barffrach01/The-Chat.git
cd The-Chat

### שלב 3: הרצת הפרויקט
docker-compose up --build

### שלב 4: גישה לאפליקציה
פתח דפדפן וגש ל:
- **HTTP**: http://localhost:8080


**הערה חשובה**: בהרצה ראשונה, הבנייה יכולה לקחת כמה דקות להורדת הדרישות והרכיבים הנדרשים.
