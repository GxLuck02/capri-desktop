# Étape 3 Mac — Section VOICE : OFF / FREE VOICE (Kokoro) / ELEVENLABS

Date : 2026-10-29. Feuille de route : `docs/mac-parity-todo.md`. Suit l'étape 2
(`2026-10-29-mac-builtin-brain-design.md`).

## But

Sur un Mac Apple Silicon, Capri **parle gratuitement en local** avec la même voix que sur Windows
(Kokoro via sherpa-onnx), et le Mainframe gagne la section VOICE de Windows : OFF / FREE VOICE /
ELEVENLABS. L'utilisateur veut rester gratuit : FREE VOICE est le palier par défaut.

Hors périmètre : micro, bouton Test, raccourcis, oreilles Parakeet (étape 4) ; choix de la voix
ElevenLabs dans le Mainframe (reste dans clic droit > Settings > Voice) ; Linux et Mac Intel (FREE VOICE
y reste indisponible : « Free voice needs a Mac with Apple silicon. »).

## Faits vérifiés

- Pack Windows `capri-voice-1.zip` (`VoicePack.Url`), 316 041 808 o, SHA-256
  `9fae085696d68a580d6b1a953d7b253129bfe506a2c016421b306b7237d3214c` (identique à `VoicePack.Sha`).
  Contenu : `model.onnx` (325,6 Mo), `voices.bin`, `tokens.txt`, `lexicon-us-en.txt`,
  `lexicon-gb-en.txt`, `espeak-ng-data/` (243 entrées), `licenses/`, plus `onnxruntime.dll` et
  `sherpa-onnx-c-api.dll` (Windows, ignorés sur Mac). Aucun chemin `..` ni absolu.
- sherpa-onnx **v1.13.8** (même version que Windows ; les structures de `LocalVoice` correspondent) :
  `sherpa-onnx-v1.13.8-osx-arm64-shared-lib.tar.bz2`, 8 773 198 o, SHA-256
  `ae77050cdae565496059d96f5ab33d77b397a0864282a4e4a26e3b3b3effb948`. Contient
  `sherpa-onnx-v1.13.8-osx-arm64-shared-lib/lib/` avec `libsherpa-onnx-c-api.dylib`,
  `libsherpa-onnx-cxx-api.dylib`, `libonnxruntime.dylib` (pas de liens symboliques). La c-api dépend de
  `@rpath/libonnxruntime.dylib` avec un rpath `@loader_path` : il suffit qu'elles soient côte à côte.
  Elle exporte `SherpaOnnxCreateOfflineTts`, `SherpaOnnxDestroyOfflineTts`,
  `SherpaOnnxOfflineTtsGenerate`, `SherpaOnnxDestroyOfflineTtsGeneratedAudio`. Signature ad hoc.
- .NET ne lit pas le bzip2 : l'archive est déballée par `/usr/bin/tar` (bsdtar, refuse `..` et les
  chemins absolus par défaut), appelé sans shell, puis vérifiée.

## Composants

### 1. `app/Voice.cs` (partagé, `#if CROSS` seulement)

- `FreeStyle` et `LocalVoice` (aujourd'hui sous `#if !CROSS`) deviennent disponibles sous `CROSS`.
  `LocalVoice` devient `partial`.
- Crochets `#if CROSS` : `Install` → `InstallMac` ; `Load` → chargement des `.dylib` du dossier ;
  `Complete` → liste Mac des fichiers requis ; vérifications `IntPtr.Size` inchangées. Windows identique.
- Un résolveur `NativeLibrary.SetDllImportResolver` fait pointer `"sherpa-onnx-c-api.dll"` vers
  `<pack>/lib/libsherpa-onnx-c-api.dylib`.

### 2. `cross/MacVoice.cs` (nouveau) — la moitié Mac de `LocalVoice`

- `Supported` : macOS arm64.
- Fichiers requis sur Mac : ceux de `VoicePack.Needed` sauf les deux `.dll`, plus
  `lib/libsherpa-onnx-c-api.dylib` et `lib/libonnxruntime.dylib`.
- `InstallMac` : espace disque (pack ×2 + libs ×4 + 64 Mo), téléchargement du pack et des libs
  (`Engine.Fetch` : SHA-256, `.part`, reprise, Cancel ; miroir de test comme l'étape 2), puis dans un
  dossier `capri-voice-1.new` :
  - zip : on n'extrait que les entrées hors `.dll`, chacune devant rester dans le dossier ;
  - tar.bz2 : `/usr/bin/tar -xjf <archive> -C <temp>` via `ArgumentList`, puis on exige que le temp ne
    contienne **que** des dossiers et des fichiers ordinaires (aucun lien), et on copie seulement
    `libsherpa-onnx-c-api.dylib` et `libonnxruntime.dylib` dans `lib/` ;
  - si tout est complet, renommage en `capri-voice-1`, suppression du zip et de l'archive.
  Toute anomalie → `IOException`, dossier temporaire supprimé, rien d'installé.
- Déchargement après 15 min sans parler (minuterie dans `Pet`, `LocalVoice.Dispose`).

### 3. `cross/Pet.cs`

- `Settings` gagne `VoiceTier` (`int?`, `null` = pas encore choisi : un ancien `cross.json` sans ce champ ne
  doit pas tomber sur OFF) et `FreeStyle` (nom). Premier
  lancement : `Silent` → 0 (OFF), clé ElevenLabs dans le Trousseau → 2, sinon 1 (FREE VOICE).
- `LocalVoice kokoro` (dossier `<données>/voice`), `FreeSpeak` (hook de test, comme Windows).
- Réponse (`Reply`) : palier 2 + clé → ElevenLabs (inchangé) ; palier 1 + pack installé + langue
  anglaise → Kokoro sur un thread, même minuterie de 8 s et même lecture (bouche synchronisée) ;
  sinon texte seul. `Speaks` suit le palier.
- `SetUpFreeVoice()` / `CancelFreeVoice()` (portés de `app/Capri.cs` ~l.3903) : progression, Cancel
  caché pendant « Loading her free voice », puis phrase test « Hey choom, this is my free voice. ».
- `ChooseVoiceTier(int)`, `ChooseFreeStyle(string)` ; OFF coupe le son en cours.
- Le menu clic droit Settings > Voice garde la gestion ElevenLabs actuelle.

### 4. `cross/Mainframe.cs` — section VOICE sous BRAIN

- Titre `VOICE`, trois boutons radio (groupe distinct des paliers BRAIN) :
  OFF · Silent · text only / FREE VOICE · Free · runs on your Mac / ELEVENLABS · Sounds amazing · paid.
- FREE VOICE : menu des styles (Her own, Bright, Sly, Sharp) ; tant que le pack manque, bouton
  **Get free voice** + taille, puis progression + Cancel ; note « Free voice speaks English only. » si
  la langue n'est pas l'anglais ; note d'erreur le cas échéant.
- ELEVENLABS : champ masqué + Connect/Forget (clé via `Keys`, comme Settings > Voice).
- Rien ne déborde (`TiersFit` étendu aux nouvelles lignes).

## Comportement

**Get free voice.** Espace disque, puis pack (316 Mo) et libs (8,7 Mo) vérifiés, déballage sûr,
« Loading her free voice » (Cancel caché), chargement de Kokoro, phrase test. Cancel pendant un
téléchargement → « Cancelled. It picks up where it left off. ». Erreur → message court sous VOICE.

**Réponse en FREE VOICE.** Kokoro chargé à la première phrase (pas au démarrage). Si la génération
dépasse 8 s, le texte s'affiche quand même. Après 15 min sans parler, Kokoro est déchargé.

**Changements.** OFF : silence immédiat. FREE VOICE sans pack : texte + Get free voice. ELEVENLABS sans
clé : champ de clé. Style : effet à la phrase suivante (Sharp recharge le dictionnaire GB).

**Langue.** Pack anglais seulement : langue ≠ anglais → texte seul + note (écart volontaire avec
Windows, qui lit quand même).

**Erreurs.** Bibliothèque ou modèle qui refuse de se charger → « Her free voice wouldn't start. Press
Get free voice again. », réponse en texte. ElevenLabs : messages actuels, pas de bascule automatique.

## Tests (`--smoke`)

Faux serveur de téléchargement de l'étape 2 (`ServeHub`) ; faux pack zip et fausse archive tar.bz2
(créée avec `/usr/bin/tar -cjf`) ; synthèse remplacée par `FreeSpeak` (renvoie un court WAV).

1. Installation : fichiers Mac requis présents, `.dll` absents, `lib/` contient les deux dylibs.
2. Sécurité : zip avec `../evil`, tar.bz2 avec `../evil` ou un lien symbolique (un fichier en trop
   est normal : la vraie archive contient `libsherpa-onnx-cxx-api.dylib`), mauvais
   SHA-256 → refusés, rien hors du dossier, pas d'installation partielle.
3. Cancel puis reprise du pack.
4. Mainframe : section VOICE, trois choix, `TiersFit`, capture `mainframe-voice.png`.
5. FREE VOICE sans pack → texte + Get free voice visible.
6. FREE VOICE avec pack → l'audio de `FreeSpeak` est joué, la bouche bouge, le style est transmis.
7. OFF → aucun son ; ELEVENLABS → champ de clé ; clé enregistrée via `Keys`.
8. Langue ≠ anglais → texte seul + note.
9. Premier lancement : `Silent` → OFF, clé → ELEVENLABS, sinon FREE VOICE.
10. Tous les tests existants passent.

**Test manuel sur le M2** : vrai téléchargement (~325 Mo), les 4 styles, délai avant la parole, RAM
dans le Moniteur d'activité (et libération après 15 min).
