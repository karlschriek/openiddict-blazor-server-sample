export function onLoad() {
    const uri = document.getElementById('qrCodeData').getAttribute('data-url');
    new QRCode(document.getElementById('qrCode'), uri);
  }