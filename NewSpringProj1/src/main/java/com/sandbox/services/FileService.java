package com.sandbox.services;

import java.io.File;

public interface FileService {
    File[] getRootFolderFiles(String diskLabel);

    File[] listRoots();

    File[] listFiles(File file);
}
