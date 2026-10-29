# Étape 2 Mac — « Set up her brain » : cerveau local intégré

Date : 2026-10-29. Feuille de route : `docs/mac-parity-todo.md`. Suit l'étape 1
(`2026-10-29-mac-mainframe-brain-design.md`).

## But

Sur un Mac Apple Silicon, Capri peut se donner un cerveau local **en un clic**, sans Ollama : elle
télécharge le moteur llama.cpp officiel et le modèle adapté à la machine, les vérifie, puis lance
`llama-server` à la demande. Comme sur Windows, Ollama reste prioritaire s'il a des modèles.

Contraintes de l'utilisateur : gratuit, **aucun processus orphelin**, sécurité et performance soignées
(M2, 8 Go).

Hors périmètre : Linux et Mac Intel (gardent Ollama ; « Set up her brain » y passe seulement par
Ollama), l'usage des yeux pour voir l'écran (étape 6 ; ils sont téléchargés dès maintenant comme sur
Windows), le palier cloud (inchangé).

## Faits vérifiés

- Release llama.cpp **b11669** (même version que Windows) :
  `llama-b11669-bin-macos-arm64.tar.gz`, 12 191 582 octets,
  SHA-256 `dbed55493d72bc1ba2babc8770d669c2289b5bc4ee9fb2a4dfc158d45b7d13fd`.
- L'archive contient un dossier `llama-b11669/` avec `llama-server`, les dylibs (dont Metal) et des
  **liens symboliques relatifs** (`libllama.0.dylib -> libllama.0.7.0.dylib`).
- `llama-server` b11669 accepte `--api-key-file`, `--sleep-idle-seconds`, `--log-disable`, `--mmproj`.
- Modèles : ceux de `Engine.Picks` (Windows), inchangés. Sur 8 Go → Gemma 4 E2B Q4_K_M
  (3 106 738 272 o) + yeux mmproj Q8_0 (557 368 064 o).

## Composants

### 1. `app/Engine.cs` (partagé, modifié a minima)

- `Engine` et `Machine` deviennent `partial` ; les parties propres à Windows (lien du zip, `llama-server.exe`,
  `Machine.Read` via kernel32/registre, Job object) passent sous `#if !CROSS`. **Le comportement
  Windows ne change pas.**

### 2. `cross/MacEngine.cs` (nouveau) — la moitié Mac d'`Engine`

- **Machine** : RAM via `sysctlbyname("hw.memsize")` (P/Invoke libc). Pas de VRAM séparée
  (mémoire unifiée). `Fit` Mac : ≥ 12 Go → Gemma 4 E4B, ≥ 7 Go → Gemma 4 E2B, sinon Qwen3 4B.
- **Moteur** : URL, taille et SHA-256 ci-dessus ; `Supported` vrai seulement sur macOS arm64.
- **Extraction sûre** du `.tar.gz` (`System.Formats.Tar` + `GZipStream`), dans un dossier
  `engine-b11669.new` puis renommé :
  - chaque entrée doit rester dans le dossier (refus de `..`, des chemins absolus) ;
  - liens symboliques acceptés seulement si leur cible est un nom simple dans le même dossier ;
  - types autorisés : dossiers, fichiers, liens symboliques ; tout le reste est refusé ;
  - `llama-server` doit exister et devient exécutable (`chmod 755`) ;
  - en cas de refus, le dossier temporaire est supprimé et rien n'est installé.
- **Lancement avec gardien** : `llama-server` est démarré par `/bin/sh` avec un **script constant**
  (aucun texte venu d'internet n'y est interpolé ; chemins et options passés en arguments
  positionnels). Le script lance le serveur, écrit son PID dans `brain/server.pid`, puis vérifie
  toutes les 2 s que Capri (PID passé en argument) vit encore ; si Capri disparaît ou si on lui envoie
  TERM, il tue le serveur, efface `server.pid` et sort.
- **Clé secrète** : 32 octets aléatoires (`RandomNumberGenerator`) en hex, écrits dans
  `brain/server.key` (permissions 600) et passés par `--api-key-file` ; jamais dans la ligne de
  commande ni dans les journaux. Effacée à l'arrêt.
- **Ménage au démarrage** : si `server.pid` désigne encore un processus `llama-server`, il est tué.
- Options du serveur : celles de Windows (`--host 127.0.0.1`, port libre au hasard, `-c` fenêtre,
  `--sleep-idle-seconds 900`, `--log-disable`, `--jinja`, etc.) ; Metal par défaut ; priorité basse.

### 3. `app/Brain.cs` (partagé, `#if CROSS`)

- Les requêtes vers le moteur intégré portent `Authorization: Bearer <clé>`. Windows inchangé.

### 4. `cross/Pet.cs`

- Un `Engine` dans `Pet` (dossier `<données>/brain`).
- **Choix au démarrage** (palier NORMAL) : modèles Ollama → Ollama ; sinon moteur intégré installé →
  `Brain.Local` (sans le démarrer) ; sinon rien et le Mainframe propose « Set up her brain ».
- `SetUpBrain()` / `CancelSetup()` portés de `app/Capri.cs` (~l.3626-3665) : Ollama qui tourne →
  `Brain.Pull` ; sinon `Engine.Install` puis premier `Ensure` ; progression limitée à 5 mises à jour/s.
- **Arrêt du serveur** : passage en HIGHER/MAX, choix d'un modèle Ollama, Quit (avant la fermeture).
- Message « hors ligne » : mentionne « Set up her brain » dans le Mainframe sur Mac Apple Silicon.

### 5. `cross/Mainframe.cs` (palier NORMAL)

- Sans cerveau : bouton **Set up her brain** + « Gemma 4 E2B · 3.7 GB » (modèle choisi + taille
  totale à télécharger). Sur Linux/Mac Intel sans Ollama : la note Ollama de l'étape 1, sans bouton.
- Pendant l'installation : texte de progression (« Downloading Gemma 4 E2B · 1.2 / 3.1 GB »),
  bouton **Cancel**.
- Installé : « Built-in · Gemma 4 E2B » / « Running on this Mac ».

## Comportement

**Set up her brain.** Espace disque vérifié (moteur ×4 + modèle + yeux + 256 Mo) sinon « Storage too
full: her brain needs X free. ». Ollama qui tourne → téléchargement du tag Ollama du modèle
(`gemma4:e2b-it-qat`) puis bascule sur Ollama. Sinon : moteur, modèle, yeux, chacun vérifié par
SHA-256 (fichier `.part`, reprise HTTP Range, 4 blocages de suite → abandon) ; puis « Loading … »,
premier démarrage du serveur, salutation.

**Cancel.** Arrête le téléchargement ; les `.part` restent → « Cancelled. It picks up where it left
off. » ; un nouveau clic reprend.

**Conversation.** Premier message → `Ensure` démarre le serveur (gardien + clé) et attend `/health`
(180 s max). 15 min sans message → le serveur libère la RAM. Charabia ou perte de Metal → relance sur
le processeur, mémorisée (logique Windows existante). Crash au chargement → « Brain crashed loading.
Close some apps, press Refresh. ».

**Fin.** Quit, palier cloud, modèle Ollama → arrêt du serveur (TERM au gardien, qui tue le serveur).
Capri tuée brutalement → le gardien tue le serveur en ≤ 2 s.

## Tests (`--smoke`)

Faux serveur HTTP local pour les téléchargements (URLs du moteur et des modèles redirigeables pour
les tests) ; faux moteur = petite archive dont `llama-server` est un script qui relance Capri en mode
`--fake-brain` (comme `--shelf`) : il sert `/health` et `/v1/chat/completions` et **exige la clé**.

1. `Fit` : 8 Go → E2B, 16 Go → E4B, 4 Go → Qwen3 4B ; `Machine.Read` réel renvoie une RAM > 0.
2. Installation depuis le faux serveur : moteur extrait, exécutable, modèle et yeux en place.
3. Mauvais SHA-256 → refusé, fichier supprimé. Archive avec `../evil` ou lien symbolique sortant →
   refusée, rien écrit hors du dossier.
4. Cancel au milieu puis reprise : le `.part` est repris, pas recommencé.
5. Premier message → serveur démarré, réponse reçue ; requête sans clé → 401 ; `server.key` effacé à
   l'arrêt.
6. Gardien : serveur lancé sous un faux parent (processus `sleep`), parent tué → serveur parti en
   moins de 3 s, `server.pid` effacé.
7. Ménage : un `server.pid` pointant vers un faux serveur vivant → tué au démarrage du moteur.
8. Passage en HIGHER et Quit → serveur arrêté.
9. Faux Ollama avec modèles → le moteur intégré n'est pas utilisé.
10. Tous les tests existants passent.

**Test manuel sur le M2** : vrai téléchargement de Gemma 4 E2B (Ollama arrêté), vitesse des réponses,
Moniteur d'activité après Quit et après `kill -9` de Capri.
