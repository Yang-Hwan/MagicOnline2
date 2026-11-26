(() => {
    'use strict';
    const form = document.getElementById('shop-edit-form');
    if (!form) return;
    const path = form.elements.namedItem('Input.Thumbnail');
    const file = form.elements.namedItem('ImageFile');
    const preview = document.getElementById('shop-image-preview');
    const empty = document.getElementById('shop-image-empty');
    const feedback = document.getElementById('shop-image-feedback');
    const keep = document.getElementById('shop-image-keep');
    const choices = [...form.querySelectorAll('[name="SelectedImage"]')];
    const initialPath = path.value;
    const initialPreview = path.dataset.preview;
    let objectUrl;

    function show(url) {
        preview.hidden = !url;
        empty.hidden = !!url;
        if (url) preview.src = url;
        else preview.removeAttribute('src');
    }
    function selectedPreview() {
        const selected = choices.find(choice => choice.checked);
        if (selected?.value === '__clear') return '';
        if (selected?.dataset.preview) return selected.dataset.preview;
        return choices.find(choice => choice.value === path.value && choice.dataset.preview)?.dataset.preview
            || (path.value === initialPath ? initialPreview : '')
            || (path.value.startsWith('/shop-images/') && !path.value.startsWith('//') ? path.value : '');
    }
    function update() {
        if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null; }
        if (file.files.length) {
            const image = file.files[0];
            if (!/\.png$/i.test(image.name) || image.size > 8 * 1024 * 1024 || image.size === 0) {
                file.setCustomValidity('8MB 이하 PNG 파일을 선택해 주세요.');
                show(''); feedback.textContent = file.validationMessage; return;
            }
            file.setCustomValidity('');
            objectUrl = URL.createObjectURL(image);
            show(objectUrl); feedback.textContent = '새 이미지 미리보기입니다. 저장하면 상품에 적용됩니다.';
        } else {
            file.setCustomValidity('');
            show(selectedPreview());
            feedback.textContent = '이미지를 고른 뒤 하단의 저장 버튼을 눌러 적용하세요.';
        }
    }
    choices.forEach(choice => choice.addEventListener('change', () => {
        if (choice.dataset.preview) path.value = choice.value;
        if (choice.value === '__clear') path.value = '';
        update();
    }));
    path.addEventListener('input', () => { keep.checked = true; update(); });
    file.addEventListener('change', update);
    preview.addEventListener('error', () => {
        preview.hidden = true; empty.hidden = false;
        feedback.textContent = '이미지를 미리 볼 수 없습니다. 파일 또는 경로를 확인해 주세요.';
    });
    window.addEventListener('pagehide', () => { if (objectUrl) URL.revokeObjectURL(objectUrl); });
    update();

    // Effective cue stats use the same external script; inline scripts remain blocked.
    const grade = form.elements.namedItem('Input.Grade');
    if (grade) {
        const updateStats = () => form.querySelectorAll('.cue-effective').forEach((cell, index) => {
            const level = +form.elements.namedItem(`Input.Initial[${index}]`).value;
            cell.textContent = Math.round(level * (grade.value === 'B' ? .9 : grade.value === 'C' ? .8 : 1)) + '%';
        });
        form.addEventListener('input', updateStats);
        updateStats();
    }
})();
