(function () {

    // Elementos do DOM controlados pelo scanner.
    // Todos declarados aqui para evitar lookups repetidos no loop de análise.
    let videoElement    = null;
    let canvasElement   = null;
    let canvasContext    = null;
    let scannerSection  = null;
    let inputField      = null;
    let activeStream    = null;
    let animationFrame  = null;

    /**
     * Inicializa o scanner conectando os elementos do DOM e registrando o evento do botão.
     * Chamado uma única vez no DOMContentLoaded.
     */
    function initialize() {
        videoElement   = document.getElementById('qr-video');
        canvasElement  = document.getElementById('qr-canvas');
        canvasContext  = canvasElement.getContext('2d', { willReadFrequently: true });
        scannerSection = document.getElementById('qr-scanner-section');
        inputField     = document.getElementById('registrationNo');

        const openButton  = document.getElementById('btn-open-scanner');
        const closeButton = document.getElementById('btn-close-scanner');

        if (!openButton) { return; }

        // Verifica se a API de câmera está disponível antes de mostrar o botão.
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            openButton.disabled = true;
            openButton.title    = 'Camera not available. Use HTTPS or enter the plate manually.';

            const fallback = document.getElementById('qr-fallback-message');
            if (fallback) { fallback.classList.remove('hidden'); }
            return;
        }

        openButton.addEventListener('click', function () {
            startCamera();
        });

        if (closeButton) {
            closeButton.addEventListener('click', function () {
                stopCamera();
            });
        }
    }

    /**
     * Solicita acesso à câmera traseira e inicia o loop de análise de frames.
     */
    function startCamera() {
        const constraints = {
            video: {
                facingMode: { ideal: 'environment' },
                width:  { ideal: 1280 },
                height: { ideal: 720 }
            }
        };

        navigator.mediaDevices.getUserMedia(constraints)
            .then(function (stream) {
                activeStream          = stream;
                videoElement.srcObject = stream;
                videoElement.setAttribute('playsinline', true);
                videoElement.play();

                scannerSection.classList.remove('hidden');

                // Inicia o loop de análise assim que o vídeo tiver dimensões.
                videoElement.addEventListener('loadedmetadata', function () {
                    requestAnimationFrame(analyzeFrame);
                });
            })
            .catch(function (error) {
                console.error('Camera access denied:', error);
                const fallback = document.getElementById('qr-fallback-message');
                if (fallback) { fallback.classList.remove('hidden'); }
            });
    }

    /**
     * Analisa um frame do vídeo em busca de um QR code.
     * Se encontrar, preenche o campo de placa e encerra a câmera.
     * Se não encontrar, agenda a análise do próximo frame.
     */
    function analyzeFrame() {
        if (videoElement.readyState !== videoElement.HAVE_ENOUGH_DATA) {
            animationFrame = requestAnimationFrame(analyzeFrame);
            return;
        }

        canvasElement.height = videoElement.videoHeight;
        canvasElement.width  = videoElement.videoWidth;
        canvasContext.drawImage(videoElement, 0, 0, canvasElement.width, canvasElement.height);

        const imageData = canvasContext.getImageData(0, 0, canvasElement.width, canvasElement.height);
        const result    = jsQR(imageData.data, imageData.width, imageData.height, {
            inversionAttempts: 'dontInvert'
        });

        if (result && result.data && result.data.trim().length > 0) {
            // QR code encontrado: preenche o campo com o valor decodificado em maiúsculas.
            inputField.value = result.data.trim().toUpperCase();
            stopCamera();
            inputField.focus();
            return;
        }

        // Nenhum QR encontrado neste frame: continua analisando.
        animationFrame = requestAnimationFrame(analyzeFrame);
    }

    /**
     * Para todos os tracks de vídeo ativos e oculta a seção do scanner.
     */
    function stopCamera() {
        if (animationFrame) {
            cancelAnimationFrame(animationFrame);
            animationFrame = null;
        }

        if (activeStream) {
            activeStream.getTracks().forEach(function (track) {
                track.stop();
            });
            activeStream = null;
        }

        if (videoElement) {
            videoElement.srcObject = null;
        }

        if (scannerSection) {
            scannerSection.classList.add('hidden');
        }
    }

    document.addEventListener('DOMContentLoaded', initialize);

})();