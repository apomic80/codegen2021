var audioControl = null;

export async function init(thisRef) {
    audioControl = new LexAudio.audioControl();
    let supported = await audioControl.supportsAudio();
    if (supported) {
        var silenceDetectionConfig = {
            time: 1500,
            amplitude: 0.2
        };

        audioControl.startRecording(
            () => onSilence(thisRef),
            null,
            silenceDetectionConfig);

        return true;
    }
    else {
        return false;
    }
}

export async function synthetizeText(request) {
    var response = await fetch('/speech/synthetizetext',
        {
            method: 'post',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(request)
        });
    var audioData = await response.blob();
    var audioPlayer = document.querySelector("audio");
    audioPlayer.src = URL.createObjectURL(audioData);
    audioPlayer.play();
}

function onSilence(thisRef) {
    audioControl.stopRecording();
    audioControl.exportWAV((blob) => {

        var data = new FormData();
        data.append('file', blob, 'audio.wav');
        fetch('/speech/recognizeaudio',
            {
                method: 'post',
                body: data
            })
            .then(result => {
                result.json().then(data => {
                    thisRef.invokeMethod(
                        'ShowResult', data);
                });
            });
    });
};