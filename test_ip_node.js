const https = require('https');

async function test() {
    const fetch = (await import('node-fetch')).default || globalThis.fetch;
    
    // 1. Get cookies
    const homeRes = await fetch("https://www.infraestruturasdeportugal.pt/negocios-e-servicos/horarios", {
        headers: {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        }
    });
    const cookie = homeRes.headers.get('set-cookie');
    console.log("Home status:", homeRes.status, "Cookie:", cookie);

    // 2. Test stations
    const stRes = await fetch("https://www.infraestruturasdeportugal.pt/negocios-e-servicos/estacao-nome/Porto", {
        headers: {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Referer": "https://www.infraestruturasdeportugal.pt/negocios-e-servicos/horarios",
            "X-Requested-With": "XMLHttpRequest",
            "Cookie": cookie || ""
        }
    });
    const stData = await stRes.json();
    console.log("Stations:", stData);

    // 3. Test departures for 9402006
    const url = "https://www.infraestruturasdeportugal.pt/negocios-e-servicos/partidas-chegadas/9402006/2026-09-01%2000:00/2026-09-01%2023:59/INTERNACIONAL,ALFA,IC,IR,REGIONAL,URB%7CSUBUR,ESPECIAL";
    console.log("Fetching:", url);
    const depRes = await fetch(url, {
        headers: {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Referer": "https://www.infraestruturasdeportugal.pt/negocios-e-servicos/horarios",
            "X-Requested-With": "XMLHttpRequest",
            "Cookie": cookie || ""
        }
    });
    console.log("Departures status:", depRes.status);
    const depText = await depRes.text();
    console.log("Departures data length:", depText.length);
    console.log("Departures sample:", depText.substring(0, 1000));
}

test();
