# Capri Mac — reproduire la version Windows

Dernière session : 2026-10-29. Repo : fork https://github.com/GxLuck02/capri-desktop
(remote `upstream` = lachydotmcg/capri-desktop). Mac M2.

## Décisions déjà prises
- On avance étape par étape, but = parité avec la version Windows (`app/`).
- Le Mainframe Mac se construit section par section, une section par étape.
- Clés API dans le Trousseau macOS (Security.framework en P/Invoke, pas la commande `security`).
  Linux garde le fichier `chmod 600`.
- Tests : suivre le système existant `--smoke` (`cross/Smoke.cs`).
- Process : spec → plan → code → test sur le M2, puis étape suivante.

## À faire avant de coder
- [ ] Installer le SDK .NET 10 : `brew install --cask dotnet-sdk`
- [ ] Vérifier que le projet compile : `dotnet build cross/Capri.Cross.csproj -c Release`
- [ ] (Optionnel) Ollama : `brew install ollama && ollama pull gemma4:e4b`

## Feuille de route
- [x] Étape 1 — Mainframe + section BRAIN + Trousseau (fait le 2026-10-29)
- [x] Étape 2 — « Set up her brain » sur Mac (llama.cpp Metal + modèle auto, Ollama en option) (fait le 2026-10-29)
- [ ] **Étape 3 — Section VOICE : OFF / FREE VOICE (Kokoro) / ELEVENLABS** ← PROCHAINE (Kokoro gratuit en priorité)
- [ ] Étape 4 — Chat vocal : micro, Test, raccourcis
- [ ] Étape 5 — Section CAPRI + page Settings (langue, surnom, personnalité, raccourcis, démarrage, Discord, Check in)
- [ ] Étape 6 — Voir l'écran
- [ ] Étape 7 — Jobs avec son curseur + Blackwall (le plus risqué)
- [ ] Étape 8 — Deux cerveaux : local privé + cloud (nouveauté, pas sur Windows). Piste retenue :
  règles fixes par tâche (tout ce qui contient une capture d'écran reste local, le texte peut aller au cloud).

## Étape 1 — où on en est
Brainstorming : partie 1/3 **validée** (2026-10-29, révisée : palier NORMAL = modèles Ollama locaux,
comme le palier 0 de Windows). Parties 2/3 et 3/3 validées, spec écrite.

Ajout validé à la partie 1/3 : NORMAL = liste des modèles Ollama installés + Refresh (réutilise
`RefreshBrains`/`PickBrain` de `cross/Pet.cs`), note « ollama pull gemma4:e2b » si Ollama absent.
Modèle local recommandé sur M2 8 Go : Gemma 4 E2B (voit l'écran). Qwen3 4B possible mais aveugle.

Partie 1/3 (composants) proposée :
1. `cross/Keys.cs` (nouveau) : `Keys.Save/Read/Forget`, Trousseau sur Mac, fichier 600 sur Linux,
   migration de l'ancien fichier `voice-key` en clair vers le Trousseau.
2. `cross/Mainframe.cs` (nouveau) : fenêtre Avalonia au look Windows (fond `Look.Panel`, titre
   MAINFRAME mono, bordures rouges), section BRAIN seule : paliers NORMAL / HIGHER / MAX,
   cerveau actif, champ clé + Connect + Free key, menu modèles + Refresh.
3. `cross/Pet.cs` (modifié) : porter `ChooseTier`, `ConnectText`, `ForgetText`, `ListCloud`
   depuis `app/Capri.cs` (~l.3690-3830). `Brain.Remote` existe déjà dans `app/Brain.cs` (partagé).
   `Settings` (cross.json) gagne `Tier`, `NvidiaModel`, `PaidModel` — jamais les clés.
4. Menu clic droit : ajouter « Capri Mainframe » ; sous-menu Brain affiche aussi les modèles cloud.

Prochaines actions :
- [x] Valider (ou ajuster) la partie 1/3
- [x] Partie 2/3 : comportement (connexion d'une clé, changement de palier, erreurs)
- [x] Partie 3/3 : tests (`--smoke`)
- [x] Écrire la spec : `docs/superpowers/specs/2026-10-29-mac-mainframe-brain-design.md` (branche `mac-step1-mainframe`) — **en attente de relecture par l'utilisateur**
- [x] Plan : `docs/superpowers/plans/2026-10-29-mac-mainframe-brain.md`
- [x] Implémentation (branche `mac-step1-mainframe`) : Keys/Trousseau, 3 paliers, fenêtre Mainframe.
  Smoke complet vert sur le M2 (2026-10-29), relecture finale faite et 2 bugs importants corrigés.
- [x] Test manuel sur le M2 : NORMAL (Ollama gemma4:e2b-it-qat), HIGHER (vraie clé NVIDIA), relance, Trousseau,
  Forget — tout OK. ElevenLabs non testé (payant, l'utilisateur veut rester gratuit au maximum).
- [x] Branche fusionnée dans main.

Remarques mineures laissées de côté (relecture finale) : appel réseau ListCloud à chaque clic droit en palier
cloud ; champ clé vidé après une erreur ; pas de note si la clé a disparu du Trousseau ; `CloseTalk` absent.

Note tests : les smoke tests ouvrent de vraies fenêtres ; Claude ne peut pas les lancer lui-même (permission
refusée) → l'utilisateur les lance avec `! cross/bin/Release/net10.0/Capri --smoke /tmp/capri-smoke`.

## Repères dans le code Windows
- Mainframe Windows : `app/Mainframe.cs` (section BRAIN ~l.194-225)
- Paliers / clés : `app/Capri.cs` ~l.3690-3830
- Fournisseurs cloud : `app/Brain.cs` (`Cloud`, `CloudModels`, `DefaultCloudModel`, `Remote`)
- Page clé NVIDIA gratuite : https://build.nvidia.com/settings/api-keys

## Étape 2 — fait
Spec `docs/superpowers/specs/2026-10-29-mac-builtin-brain-design.md`, plan `docs/superpowers/plans/2026-10-29-mac-builtin-brain.md`.
Moteur llama.cpp b11669 (SHA-256), décompression sûre, gardien anti-orphelin, clé secrète --api-key-file,
Ollama prioritaire. Smoke 38 PASS. Test manuel M2 OK : 1re réponse 7 s, ensuite 1-2 s ; Quit et kill -9
ne laissent aucun llama-server ; Ollama reprend la main quand il tourne. Cancel en cours : couvert par smoke.

Points mineurs laissés (relecture finale) : Crashed/Running regardent le gardien (crash du serveur vu
avec ≤ 2 s de retard, relance CPU rare sur Mac) ; Sweep ne vérifie que le nom du processus (PID réutilisé
ou 2e instance de Capri) ; Stop peut bloquer l'UI quelques secondes si llama-server ignore TERM ;
un Ensure tardif après DropBuiltIn peut relancer un serveur inutilisé (fenêtre étroite).

## Étape 3 — où on en est
Design validé (3 parties), spec : `docs/superpowers/specs/2026-10-29-mac-free-voice-design.md`
(branche `mac-step3-voice`) — en attente de relecture utilisateur, puis plan.

## Notes pour la suite
- L'utilisateur veut rester **gratuit au maximum** : privilégier local (Ollama / llama.cpp, Kokoro) et NVIDIA gratuit.
- Sur le M2 8 Go, utiliser `gemma4:e2b-it-qat` (4,3 Go). Le `gemma4:e2b` par défaut d'Ollama fait 7,5 Go : trop gros.
- Données Capri sur Mac : `~/Library/Application Support/Capri/` (pas ~/.config).
- Clic droit sur Capri = deux doigts sur le trackpad.
